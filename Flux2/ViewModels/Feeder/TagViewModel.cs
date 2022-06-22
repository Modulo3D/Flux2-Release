using DynamicData;
using DynamicData.Kernel;
using Modulo3DDatabase;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public abstract class TagViewModel<TTagViewModel, TNFCTag, TDocument, TState> : RemoteControl<TTagViewModel>, IFluxTagViewModel<TNFCTag, TDocument, TState>
        where TTagViewModel : TagViewModel<TTagViewModel, TNFCTag, TDocument, TState>
        where TNFCTag : INFCOdometerTag<TNFCTag>
    {
        public ushort Position { get; }
        public FluxViewModel Flux { get; }
        public abstract TState State { get; }
        public FeederViewModel Feeder { get; }
        public abstract ushort VirtualTagId { get; }
        public string VirtualCardId => $"00-00-{VirtualTagId:00}-{Position:00}";
        IFluxFeederViewModel IFluxTagViewModel.Feeder => Feeder;
        public abstract OdometerViewModel<TNFCTag> Odometer { get; }

        INFCReading IFluxTagViewModel.Nfc => Nfc;
        private NFCReading<TNFCTag> _Nfc;
        public NFCReading<TNFCTag> Nfc
        {
            get => _Nfc;
            set => this.RaiseAndSetIfChanged(ref _Nfc, value);
        }

        private readonly ObservableAsPropertyHelper<TDocument> _Document;
        public TDocument Document => _Document.Value;


        [RemoteOutput(true)]
        public abstract Optional<string> DocumentLabel { get; }

        private ObservableAsPropertyHelper<double> _OdometerPercentage;
        [RemoteOutput(true)]
        public double OdometerPercentage => _OdometerPercentage.Value;

        private ObservableAsPropertyHelper<Optional<double>> _RemainingWeight;
        [RemoteOutput(true, typeof(WeightConverter))]
        public Optional<double> RemainingWeight => _RemainingWeight.Value;

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> UpdateTagCommand { get; internal set; }

        public TagViewModel(
            FeederViewModel feeder, ushort position,
            Func<FluxUserSettings, SourceCache<NFCReading, ushort>> get_tag_storage,
            Func<ILocalDatabase, TNFCTag, TDocument> find_document,
            Func<TNFCTag, Guid> check_tag) : base($"{typeof(TTagViewModel).GetRemoteControlName()}??{position}")
        {
            Feeder = feeder;
            Flux = feeder.Flux;
            Position = position;
            CheckTag = check_tag;

            _Document = Observable.CombineLatest(
                Feeder.Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                this.WhenAnyValue(v => v.Nfc),
                (db, nfc) => (db, nfc))
                .Select(tuple =>
                {
                    if (!tuple.db.HasValue)
                        return default;
                    if (!tuple.nfc.Tag.HasValue)
                        return default;
                    return find_document(tuple.db.Value, tuple.nfc.Tag.Value);
                })
                .ToProperty(this, vm => vm.Document)
                .DisposeWith(Disposables);

            var core_settings = Flux.SettingsProvider.CoreSettings;
            var user_settings = Flux.SettingsProvider.UserSettings;
            var printer_guid = core_settings.Local.PrinterGuid;
            var tag_storage = get_tag_storage(user_settings.Local);

            this.WhenAnyValue(v => v.Nfc)
                .Throttle(TimeSpan.FromSeconds(0.1))
                .Subscribe(nfc =>
                {
                    if (nfc.Tag.HasValue && nfc.CardId.HasValue)
                    {
                        var tag = nfc.Tag.Value;
                        var card_id = nfc.CardId.Value;
                        if (tag.PrinterGuid == printer_guid)
                        {
                            tag_storage.AddOrUpdate(new NFCReading(card_id, Position));
                        }
                    }
                    else
                    {
                        tag_storage.RemoveKey(Position);
                    }
                    user_settings.PersistLocalSettings();
                });

            UpdateTagCommand = ReactiveCommand.CreateFromTask(async () =>
                {
                    var operator_usb = Flux.MCodes.OperatorUSB;
                    var reading = await Flux.UseReader(h => ReadTag(h, true), r => r.HasValue);

                    if (!reading.HasValue)
                    {
                        if (operator_usb.ConvertOr(o => o.RewriteNFC, () => false))
                        {
                            var tag = await CreateTagAsync();
                            reading = new NFCReading<TNFCTag>(tag, VirtualCardId);
                        }

                        if (!reading.HasValue)
                            return;
                    }

                    await StoreTagAsync(reading.Value);
                },
                Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.CanSafeCycle),
                RxApp.MainThreadScheduler)
                .DisposeWith(Disposables);

            var stored_reading = tag_storage.Lookup(Position);
            if (stored_reading.HasValue)
            { 
                var backup_reading = stored_reading.Value.CardId.Convert(id => ReadBackupTag(id, check_tag));
                if (backup_reading.HasValue)
                {
                    var backup = backup_reading.Value;
                    if (backup.Tag.HasValue)
                    {
                        var tag = backup.Tag.Value;
                        if (tag.PrinterGuid == printer_guid)
                            Nfc = backup;
                    }
                }
            }
        }

        public virtual void Initialize() 
        {
            _OdometerPercentage = Odometer.WhenAnyValue(v => v.Percentage)
                .ToProperty(this, v => v.OdometerPercentage)
                .DisposeWith(Disposables);

            _RemainingWeight = this.WhenAnyValue(v => v.Nfc)
                .Select(d => d.Tag)
                .Convert(n => n.CurWeightG)
                .ToProperty(this, v => v.RemainingWeight)
                .DisposeWith(Disposables);
        }

        public Task DisconnectAsync()
        {
            Nfc = default;
            return Task.CompletedTask;
        }
        
        public abstract Task<Optional<TNFCTag>> CreateTagAsync();


        // Backup nfc
        public Func<TNFCTag, Guid> CheckTag { get; }
        private object SyncRoot { get; } = new object();
        public void StoreTag(Func<TNFCTag, TNFCTag> modify_tag)
        {
            if (!Nfc.Tag.HasValue)
                return;

            Nfc = Nfc.SetTag(modify_tag);
            if (!WriteBackupTag(Nfc))
                Flux.Messages.LogMessage("Errore di blocco tag", "Backup non effettuato", MessageLevel.ERROR, 37001);
        }
        protected bool WriteBackupTag(INFCReading<TNFCTag> reading)
        {
            try
            {
                lock (SyncRoot)
                {
                    var nfc_file = Path.Combine(Directories.NFCBackup.FullName, $"{reading.CardId}");
                    var tmp_file = Path.Combine(Directories.NFCBackup.FullName, $"{reading.CardId}.tmp");
                    var bkp_file = Path.Combine(Directories.NFCBackup.FullName, $"{reading.CardId}.bkp");

                    var result = reading.Tag.Serialize(new FileInfo(tmp_file));

                    if (result)
                    {
                        if (File.Exists(bkp_file))
                            File.Delete(bkp_file);
                        File.Copy(tmp_file, bkp_file, true);

                        if (File.Exists(nfc_file))
                            File.Delete(nfc_file);
                        File.Copy(bkp_file, nfc_file, true);

                        File.Delete(tmp_file);
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return false;
            }
        }
        protected bool DeleteBackupTag(INFCReading<TNFCTag> tag)
        {
            try
            {
                lock (SyncRoot)
                {
                    var nfc_file = Path.Combine(Directories.NFCBackup.FullName, $"{tag.CardId}");
                    var tmp_file = Path.Combine(Directories.NFCBackup.FullName, $"{tag.CardId}.tmp");
                    var bkp_file = Path.Combine(Directories.NFCBackup.FullName, $"{tag.CardId}.bkp");

                    if (File.Exists(nfc_file))
                        File.Delete(nfc_file);
                    if (File.Exists(tmp_file))
                        File.Delete(tmp_file);
                    if (File.Exists(bkp_file))
                        File.Delete(bkp_file);
                }

                return true;
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return false;
            }
        }
        public async Task<NFCReading<TNFCTag>> StoreTagAsync(NFCReading<TNFCTag> nfc)
        {
            foreach (var feeder in Feeder.Feeders.Feeders.Items)
            {
                var nozzle = feeder.ToolNozzle.Nfc;
                var result = check_tag(nozzle, Position);
                if (result.HasValue)
                {
                    if (result.Value)
                        continue;
                    return nfc;
                }
                await feeder.ToolNozzle.DisconnectAsync();
            }

            foreach (var feeder in Feeder.Feeders.Feeders.Items)
            {
                if (!feeder.SelectedMaterial.HasValue)
                    continue;
                var material_nfc = feeder.SelectedMaterial.Value.Nfc;
                var result = check_tag(material_nfc, Position);
                if (result.HasValue)
                {
                    if (result.Value)
                        continue;
                    return nfc;
                }
                await feeder.SelectedMaterial.Value.DisconnectAsync();
            }

            Nfc = nfc;
            var printer_guid = Flux.SettingsProvider.CoreSettings.Local.PrinterGuid;
            if (Nfc.CardId.HasValue && Nfc.Tag.HasValue && Nfc.Tag.Value.PrinterGuid == printer_guid && !WriteBackupTag(Nfc))
                Flux.Messages.LogMessage("Errore di aggiornamento tag", "Backup non effettuato", MessageLevel.ERROR, 38001);

            return nfc;

            Optional<bool> check_tag<TNFCTag2>(INFCReading<TNFCTag2> reading, ushort tag_position) where TNFCTag2 : INFCTag
            {
                if (reading.CardId != nfc.CardId)
                    return true;

                if (tag_position == Position)
                    return true;

                if (!reading.Tag.HasValue || reading.Tag.Value.Loaded.HasValue || reading.Tag.Value.PrinterGuid != Guid.Empty)
                    return false;

                return default;
            }
        }
        public void StoreTag(Func<INFCTag, INFCTag> modify_tag) => StoreTag(t => (TNFCTag)modify_tag(t));
        protected NFCReading<TNFCTag> ReadBackupTag(string card_id, Func<TNFCTag, Guid> check_backup_tag)
        {
            try
            {
                lock (SyncRoot)
                {
                    var nfc_file = Path.Combine(Directories.NFCBackup.FullName, $"{card_id}");
                    var tmp_file = Path.Combine(Directories.NFCBackup.FullName, $"{card_id}.tmp");
                    var bkp_file = Path.Combine(Directories.NFCBackup.FullName, $"{card_id}.bkp");

                    if (load_from_file(nfc_file, out var local))
                        return new NFCReading<TNFCTag>(local, card_id);
                    else if (load_from_file(tmp_file, out var temp))
                        return new NFCReading<TNFCTag>(temp, card_id);
                    else if (load_from_file(bkp_file, out var backup))
                        return new NFCReading<TNFCTag>(backup, card_id);
                    else return default;
                }

                bool load_from_file(string path, out TNFCTag local)
                {
                    local = default;
                    var file = new FileInfo(path);

                    if (!file.Exists)
                        return false;

                    if (file.Length == 0)
                        return false;

                    var optional_local = JsonUtils.Deserialize<TNFCTag>(file);
                    if (optional_local.HasValue)
                    {
                        local = optional_local.Value;
                        var check = check_backup_tag(local);
                        return check != Guid.Empty;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
                return default;
            }
        }

        // Nfc
        private Optional<string> ReadCardId(Optional<INFCHandle> handle)
        {
            var operator_usb = Flux.MCodes.OperatorUSB;
            var card_id = handle.Convert(h => h.GetCardId());

            if (!card_id.HasValue)
            {
                if (!operator_usb.ConvertOr(o => o.RewriteNFC, () => false))
                    return default;
                return $"00-00-{VirtualTagId:00}-{Position:00}";
            }

            return card_id;
        }
        public async Task<bool> LockTagAsync(Optional<INFCHandle> handle)
        {
            var settings = Flux.SettingsProvider.CoreSettings.Local;

            // check current tag
            var card_id = Nfc.IsVirtualTag ? Nfc.CardId : ReadCardId(handle);
            if (!card_id.HasValue)
            {
                Flux.Messages.LogMessage("Errore blocco tag", "Tag non trovato, controllare la distanza dal lettore nfc", MessageLevel.INFO, 33000);
                return false;
            }

            if (card_id != Nfc.CardId)
            {
                Flux.Messages.LogMessage("Errore blocco tag", $"Id del tag non corretto {card_id} -> {Nfc.CardId}", MessageLevel.INFO, 33001);
                return false;
            }

            if (!Nfc.Tag.HasValue)
            {
                Flux.Messages.LogMessage("Errore blocco tag", "Dati del tag non trovati, controllare la corretta scrittura", MessageLevel.INFO, 33002);
                return false;
            }

            if (Nfc.Tag.Value.PrinterGuid != Guid.Empty)
            {
                Flux.Messages.LogMessage("Errore di blocco tag", "Tag già bloccato", MessageLevel.INFO, 33003);
                return false;
            }

            // Write locked tag
            await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(settings.PrinterGuid)));
            if (!Nfc.IsVirtualTag)
            {
                if (!handle.HasValue)
                {
                    Flux.Messages.LogMessage("Errore di blocco tag", "Impossibile trovare un lettore nfc", MessageLevel.ERROR, 33004);
                    await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(default)));
                    return false;
                }

                if (!handle.Value.NDEFWrite(Nfc.Tag.Value))
                {
                    Flux.Messages.LogMessage("Errore di blocco tag", "Impossibile scrivere il tag, controllare la distanza dal lettore nfc", MessageLevel.INFO, 33004);
                    await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(default)));
                    return false;
                }
            }

            // Create backup
            if (!WriteBackupTag(Nfc))
            {
                Flux.Messages.LogMessage("Errore di blocco tag", "Backup non effettuato", MessageLevel.INFO, 33003);
                return false;
            }

            // check written tag
            var written_tag = Nfc.IsVirtualTag ? Optional.Some(Nfc) : ReadTag(handle, false);
            if (!written_tag.HasValue)
            {
                Flux.Messages.LogMessage("Errore di blocco tag", "Errore durante la lettura del tag", MessageLevel.INFO, 33005);
                await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(default)));
                return false;
            }

            using var sha256 = SHA256.Create();
            var source_hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(Nfc.Serialize())).ToHex();
            var written_hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(written_tag.Value.Serialize())).ToHex();

            if (source_hash != written_hash)
            {
                Flux.Messages.LogMessage("Errore di blocco tag", "Errore durante la scrittura del tag", MessageLevel.INFO, 33006);
                await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(default)));
                return false;
            }
            return true;
        }
        public async Task<bool> UnlockTagAsync(Optional<INFCHandle> handle)
        {
            var settings = Flux.SettingsProvider.CoreSettings.Local;

            // check current tag
            var card_id = Nfc.IsVirtualTag ? Nfc.CardId : ReadCardId(handle);
            if (!card_id.HasValue)
            {
                Flux.Messages.LogMessage("Errore sblocco tag", "Tag non trovato, controllare la distanza dal lettore nfc", MessageLevel.INFO, 34000);
                return false;
            }

            if (card_id != Nfc.CardId)
            {
                Flux.Messages.LogMessage("Errore sblocco tag", "Id del tag non corretto", MessageLevel.INFO, 34001);
                return false;
            }

            if (!Nfc.Tag.HasValue)
            {
                Flux.Messages.LogMessage("Errore blocco tag", "Dati del tag non trovati, controllare la corretta scrittura", MessageLevel.INFO, 34002);
                return false;
            }

            // Already unlocked
            if (Nfc.Tag.Value.PrinterGuid == Guid.Empty)
                return true;

            if (Nfc.Tag.Value.PrinterGuid != settings.PrinterGuid)
            {
                Flux.Messages.LogMessage("Errore sblocco tag", "Tag non appartenente alla stampante, contattare l'assistenza", MessageLevel.INFO, 34003);
                return false;
            }

            // Write unlocked tag
            await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(default)));
            if (!Nfc.IsVirtualTag)
            {
                if (!handle.HasValue)
                {
                    Flux.Messages.LogMessage("Errore di sblocco tag", "Impossibile trovare un lettore nfc", MessageLevel.ERROR, 33004);
                    await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(default)));
                    return false;
                }

                if (!handle.Value.NDEFWrite(Nfc.Tag.Value))
                {
                    Flux.Messages.LogMessage("Errore di sblocco tag", "Impossibile scrivere il tag, controllare la distanza dal lettore nfc", MessageLevel.INFO, 34004);
                    await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(settings.PrinterGuid)));
                    return false;
                }
            }

            // check written tag
            var written_tag = Nfc.IsVirtualTag ? Optional.Some(Nfc) : ReadTag(handle, false);
            if (!written_tag.HasValue)
            {
                Flux.Messages.LogMessage("Errore di sblocco tag", "Errore durante la lettura del tag", MessageLevel.INFO, 33005);
                await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(default)));
                return false;
            }

            using var sha256 = SHA256.Create();
            var source_hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(Nfc.Serialize())).ToHex();
            var written_hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(written_tag.Value.Serialize())).ToHex();

            if (source_hash != written_hash)
            {
                Flux.Messages.LogMessage("Errore di sblocco tag", "Errore durante la scrittura del tag", MessageLevel.INFO, 34006);
                await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(settings.PrinterGuid)));
                return false;
            }

            // Delete backup
            if (!DeleteBackupTag(Nfc))
            {
                Flux.Messages.LogMessage("Errore di sblocco tag", "Impossibile cancellare il backup", MessageLevel.INFO, 34007);
                await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(settings.PrinterGuid)));
                return false;
            }

            await DisconnectAsync();
            return true;
        }
        public virtual Optional<NFCReading<TNFCTag>> ReadTag(Optional<INFCHandle> handle, bool read_from_backup)
        {
            var virtual_tag = false;
            var operator_usb = Flux.MCodes.OperatorUSB;

            var card_id = handle.Convert(h => h.GetCardId());
            if (!card_id.HasValue)
            {
                if (!operator_usb.ConvertOr(o => o.RewriteNFC, () => false))
                    return default;

                virtual_tag = true;
                card_id = $"00-00-{VirtualTagId:00}-{Position:00}";
            }

            if (virtual_tag || read_from_backup)
            {
                var core_settings = Flux.SettingsProvider.CoreSettings;
                var printer_guid = core_settings.Local.PrinterGuid;
                var backup = ReadBackupTag(card_id.Value, CheckTag);
                if (backup.CardId.HasValue && backup.Tag.HasValue)
                    if (backup.Tag.Value.PrinterGuid == printer_guid)
                        return Optional.Some(backup);
                if (virtual_tag)
                    return default;
            }

            var reading = handle.Convert(h => h.NDEFRead<TNFCTag>());

            var type = reading.Convert(r => r.type);
            var expected_type = typeof(TNFCTag).GetLowerCaseName();
            if (!type.HasValue || type.Value != expected_type)
            {
                Flux.Messages.LogMessage("Errore di lettura", $"Tipo del tag non corretto: {type} invece di {expected_type}", MessageLevel.INFO, 36001);
                return default;
            }

            var tag = reading.Convert(r => r.tag);
            if (!tag.HasValue)
            {
                Flux.Messages.LogMessage("Errore di lettura", "Nessun dato trovato", MessageLevel.INFO, 36006);
                return default;
            }

            var settings = Flux.SettingsProvider.CoreSettings.Local;
            if (tag.Value.PrinterGuid != Guid.Empty && tag.Value.PrinterGuid != settings.PrinterGuid)
            {
                Flux.Messages.LogMessage("Errore di lettura", "Tag bloccato", MessageLevel.INFO, 36006);
                return default;
            }

            return new NFCReading<TNFCTag>(tag.Value, card_id.Value);
        }
    }
}
