using DynamicData;
using DynamicData.Kernel;
using Modulo3DDatabase;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public abstract class TagViewModel<TNFCTag, TDocument, TState> : ReactiveObject, IFluxTagViewModel<TNFCTag, TDocument, TState>
         where TNFCTag : INFCOdometerTag<TNFCTag>
    {
        public FluxViewModel Flux { get; }
        public abstract TState State { get; }
        public FeederViewModel Feeder { get; }
        public abstract int VirtualTagId { get; }
        IFluxFeederViewModel IFluxTagViewModel.Feeder => Feeder;
        public abstract OdometerViewModel<TNFCTag> Odometer { get; }

        INFCReading IFluxTagViewModel.Nfc => Nfc;
        private NFCReading<TNFCTag> _Nfc;
        public NFCReading<TNFCTag> Nfc
        {
            get => _Nfc;
            set => this.RaiseAndSetIfChanged(ref _Nfc, value);
        }

        private ObservableAsPropertyHelper<Optional<NFCReaderHandle>> _Reader;
        public Optional<NFCReaderHandle> Reader => _Reader.Value;

        private readonly ObservableAsPropertyHelper<TDocument> _Document;
        public TDocument Document => _Document.Value;

        public TagViewModel(
            FeederViewModel feeder,
            Func<FluxUserSettings, SourceCache<NFCReading, ushort>> get_tag_storage,
            Func<ILocalDatabase, TNFCTag, TDocument> find_document,
            Func<TNFCTag, Guid> check_tag)
        {
            Feeder = feeder;
            Flux = feeder.Flux;
            CheckTag = check_tag;

            _Reader = GetReader()
                .ToProperty(this, v => v.Reader);

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
                .ToProperty(this, vm => vm.Document);

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
                            tag_storage.AddOrUpdate(new NFCReading(card_id, Feeder.Position));
                        }
                    }
                    else
                    {
                        tag_storage.RemoveKey(Feeder.Position);
                    }
                    user_settings.PersistLocalSettings();
                });

            var stored_reading = tag_storage.Lookup(Feeder.Position);
            if (!stored_reading.HasValue)
                return;

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

        public abstract void Initialize();

        public async Task DisconnectAsync()
        {
            await CloseAsync();
            Nfc = default;
        }

        protected abstract IObservable<Optional<NFCReaderHandle>> GetReader();
        protected abstract Task<(bool result, Optional<TNFCTag> tag)> CreateTagAsync(string card_id, bool virtual_tag);

        public async Task<bool> LockTagAsync()
        {
            var settings = Flux.SettingsProvider.CoreSettings.Local;

            // check current tag
            var card_id = await ReadCardId();
            if (!card_id.HasValue)
            {
                Flux.Messages.LogMessage("Errore blocco tag", "Tag non trovato, controllare la distanza dal lettore nfc", MessageLevel.ERROR, 33000);
                return false;
            }

            if (card_id != Nfc.CardId)
            {
                Flux.Messages.LogMessage("Errore blocco tag", $"Id del tag non corretto {card_id} -> {Nfc.CardId}", MessageLevel.ERROR, 33001);
                return false;
            }

            if (!Nfc.Tag.HasValue)
            {
                Flux.Messages.LogMessage("Errore blocco tag", "Dati del tag non trovati, controllare la corretta scrittura", MessageLevel.ERROR, 33002);
                return false;
            }

            if (Nfc.Tag.Value.PrinterGuid != Guid.Empty)
            {
                Flux.Messages.LogMessage("Errore di blocco tag", "Tag già bloccato", MessageLevel.ERROR, 33003);
                return false;
            }

            // Write locked tag
            await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(settings.PrinterGuid)));
            if (!Nfc.IsVirtualTag && !await WriteTagAsync(Nfc.Tag.Value))
            {
                Flux.Messages.LogMessage("Errore di blocco tag", "Impossibile scrivere il tag, controllare la distanza dal lettore nfc", MessageLevel.ERROR, 33004);
                await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(default)));
                return false;
            }

            // Create backup
            if (!WriteBackupTag(Nfc))
            {
                Flux.Messages.LogMessage("Errore di blocco tag", "Backup non effettuato", MessageLevel.ERROR, 33003);
                return false;
            }

            // check written tag
            var written_tag = Nfc.IsVirtualTag ? Optional.Some(Nfc) : await ReadTagAsync(false, false);
            if (!written_tag.HasValue)
            {
                Flux.Messages.LogMessage("Errore di blocco tag", "Errore durante la lettura del tag", MessageLevel.ERROR, 33005);
                await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(default)));
                return false;
            }

            using var sha256 = SHA256.Create();
            var source_hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(Nfc.Serialize())).ToHex();
            var written_hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(written_tag.Value.Serialize())).ToHex();

            if (source_hash != written_hash)
            {
                Flux.Messages.LogMessage("Errore di blocco tag", "Errore durante la scrittura del tag", MessageLevel.ERROR, 33006);
                await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(default)));
                return false;
            }

            return true;
        }
        public async Task<bool> UnlockTagAsync()
        {
            var settings = Flux.SettingsProvider.CoreSettings.Local;

            // check current tag
            var card_id = await ReadCardId();
            if (!card_id.HasValue)
            {
                Flux.Messages.LogMessage("Errore sblocco tag", "Tag non trovato, controllare la distanza dal lettore nfc", MessageLevel.ERROR, 34000);
                return false;
            }

            if (card_id != Nfc.CardId)
            {
                Flux.Messages.LogMessage("Errore sblocco tag", "Id del tag non corretto", MessageLevel.ERROR, 34001);
                return false;
            }

            if (!Nfc.Tag.HasValue)
            {
                Flux.Messages.LogMessage("Errore blocco tag", "Dati del tag non trovati, controllare la corretta scrittura", MessageLevel.ERROR, 34002);
                return false;
            }

            // Already unlocked
            if (Nfc.Tag.Value.PrinterGuid == Guid.Empty)
                return true;

            if (Nfc.Tag.Value.PrinterGuid != settings.PrinterGuid)
            {
                Flux.Messages.LogMessage("Errore sblocco tag", "Tag non appartenente alla stampante, contattare l'assistenza", MessageLevel.ERROR, 34003);
                return false;
            }

            // Write unlocked tag
            await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(default)));
            if (!Nfc.IsVirtualTag && !await WriteTagAsync(Nfc.Tag.Value))
            {
                Flux.Messages.LogMessage("Errore di sblocco tag", "Impossibile scrivere il tag, controllare la distanza dal lettore nfc", MessageLevel.ERROR, 34004);
                await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(settings.PrinterGuid)));
                return false;
            }

            // check written tag
            var written_tag = Nfc.IsVirtualTag ? Optional.Some(Nfc) : await ReadTagAsync(read_from_backup: false, create_tag: false);
            if (!written_tag.HasValue)
            {
                Flux.Messages.LogMessage("Errore di sblocco tag", "Errore durante la lettura del tag", MessageLevel.ERROR, 33005);
                await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(default)));
                return false;
            }

            using var sha256 = SHA256.Create();
            var source_hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(Nfc.Serialize())).ToHex();
            var written_hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(written_tag.Value.Serialize())).ToHex();

            if (source_hash != written_hash)
            {
                Flux.Messages.LogMessage("Errore di sblocco tag", "Errore durante la scrittura del tag", MessageLevel.ERROR, 34006);
                await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(settings.PrinterGuid)));
                return false;
            }

            // Delete backup
            if (!DeleteBackupTag(Nfc))
            {
                Flux.Messages.LogMessage("Errore di sblocco tag", "Impossibile cancellare il backup", MessageLevel.ERROR, 34007);
                await StoreTagAsync(Nfc.SetTag(t => t.SetPrinterGuid(settings.PrinterGuid)));
                return false;
            }

            await DisconnectAsync();
            return true;
        }

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
        protected async Task<UFR_STATUS> CloseAsync()
        {
            return await Task.Run(async () =>
            {
                if (!Reader.HasValue)
                    return UFR_STATUS.UNKNOWN_ERROR;
                return await Reader.Value.CloseAsync(TimeSpan.FromSeconds(5));
            });
        }
        private async Task<Optional<string>> ReadCardId()
        {
            if (Nfc.IsVirtualTag)
                return Nfc.CardId;

            return await Task.Run(async () =>
            {
                var card_id = await Reader.ConvertAsync(
                    async r => await r.OpenAsync(h => read_card(h), TimeSpan.FromSeconds(5)));
                if (card_id.HasValue)
                    return card_id.Value;
                return await read_card(default);
            });

            async Task<Optional<string>> read_card(Optional<NFCReaderHandle> handle)
            {
                var operator_usb = Flux.MCodes.OperatorUSB;
                var card_id = await handle.ConvertAsync(h => h.WaitForTagAsync(TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(10)));

                if (!card_id.HasValue)
                {
                    if (!operator_usb.ConvertOr(o => o.RewriteNFC, () => false))
                    {
                        Flux.Messages.LogMessage("Errore di lettura", "Nessun tag trovato", MessageLevel.ERROR, 36002);
                        return default;
                    }

                    return $"00-00-{VirtualTagId:00}-{Feeder.Position:00}";
                }

                return card_id;
            }
        }
        protected async Task<bool> WriteTagAsync(TNFCTag tag)
        {
            if (!Reader.HasValue)
            {
                Flux.Messages.LogMessage("Errore di scrittura", "Nessun lettore trovato", MessageLevel.ERROR, 35001);
                return false;
            }

            return await Task.Run(async () =>
            {
                var result = await Reader.Value.OpenAsync(async handle =>
                {
                    var tag_id = await handle.WaitForTagAsync(TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(10));
                    if (!tag_id.HasValue)
                    {
                        Flux.Messages.LogMessage("Errore di scrittura", "Nessun tag trovato", MessageLevel.ERROR, 35002);
                        return false;
                    }

                    if (!handle.NDEFWrite(tag))
                    {
                        Flux.Messages.LogMessage("Errore di scrittura", "Tag non scritto", MessageLevel.ERROR, 35003);
                        return false;
                    }

                    return true;
                }, TimeSpan.FromSeconds(5));

                return result.ValueOr(() => false);
            });
        }
        public virtual async Task<Optional<NFCReading<TNFCTag>>> ReadTagAsync(bool read_from_backup, bool create_tag)
        {
            return await Task.Run(async () =>
            {
                return await Reader.ConvertOr(
                    r => r.OpenAsync(h => get_tag_async(h), TimeSpan.FromSeconds(5)),
                    () => get_tag_async(default));
            });

            async Task<Optional<NFCReading<TNFCTag>>> get_tag_async(Optional<NFCReaderHandle> handle)
            {
                var virtual_tag = false;
                var operator_usb = Flux.MCodes.OperatorUSB;

                var card_id = await handle.ConvertAsync(h => h.WaitForTagAsync(TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(10)));
                if (!card_id.HasValue)
                {
                    if (handle.HasValue && !operator_usb.ConvertOr(o => o.RewriteNFC, () => false))
                    {
                        Flux.Messages.LogMessage("Errore di lettura", "Nessun tag trovato", MessageLevel.ERROR, 36002);
                        return default;
                    }

                    virtual_tag = true;
                    card_id = $"00-00-{VirtualTagId:00}-{Feeder.Position:00}";
                }

                if (virtual_tag || read_from_backup)
                {
                    var core_settings = Flux.SettingsProvider.CoreSettings;
                    var printer_guid = core_settings.Local.PrinterGuid;
                    var backup = ReadBackupTag(card_id.Value, CheckTag);
                    if (backup.CardId.HasValue && backup.Tag.HasValue)
                        if (backup.Tag.Value.PrinterGuid == printer_guid)
                            return Optional.Some(backup);
                }

                var reading = handle.Convert(h => h.NDEFRead<TNFCTag>());
                var tag = reading.Convert(r => r.tag);
                var type = reading.Convert(r => r.type);

                var expected_type = typeof(TNFCTag).GetLowerCaseName();
                if (type.HasValue && !string.IsNullOrEmpty(type.Value) && type.Value != expected_type)
                {
                    Flux.Messages.LogMessage("Errore di lettura", $"Tipo del tag non corretto: {type} invece di {expected_type}", MessageLevel.ERROR, 36001);
                    return default;
                }

                if (!tag.HasValue)
                {
                    if (!operator_usb.ConvertOr(o => o.RewriteNFC, () => false) || !create_tag)
                        return default;

                    var created_tag = await CreateTagAsync(card_id.Value, virtual_tag);
                    if (!created_tag.result)
                        return default;

                    tag = created_tag.tag;
                    if (!tag.HasValue)
                        return new NFCReading<TNFCTag>(tag, card_id.Value);

                    if (!virtual_tag)
                    {
                        if (!handle.HasValue)
                        {
                            Flux.Messages.LogMessage("Errore di lettura", "Nessun lettore trovato", MessageLevel.ERROR, 36001);
                            return default;
                        }

                        if (handle.Value.NDEFInitialize() != UFR_STATUS.UFR_OK || !handle.Value.NDEFWrite(tag.Value))
                        {
                            Flux.Messages.LogMessage("Errore di scrittura", "Tag non inizializzato", MessageLevel.ERROR, 36005);
                            return default;
                        }
                    }
                }

                var settings = Flux.SettingsProvider.CoreSettings.Local;
                if (tag.Value.PrinterGuid != Guid.Empty && tag.Value.PrinterGuid != settings.PrinterGuid)
                {
                    Flux.Messages.LogMessage("Errore di lettura", "Tag bloccato", MessageLevel.ERROR, 36006);
                    return default;
                }

                return new NFCReading<TNFCTag>(tag.Value, card_id.Value);
            }
        }

        public async Task<NFCReading<TNFCTag>> StoreTagAsync(NFCReading<TNFCTag> nfc)
        {
            foreach (var feeder in Feeder.Feeders.Feeders.Items)
            {
                var nozzle = feeder.ToolNozzle.Nfc;
                var result = check_tag(nozzle, feeder.Position);
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
                var material = feeder.Material.Nfc;
                var result = check_tag(material, feeder.Position);
                if (result.HasValue)
                {
                    if (result.Value)
                        continue;
                    return nfc;
                }
                await feeder.Material.DisconnectAsync();
            }

            Nfc = nfc;
            var printer_guid = Flux.SettingsProvider.CoreSettings.Local.PrinterGuid;
            if (Nfc.CardId.HasValue && Nfc.Tag.HasValue && Nfc.Tag.Value.PrinterGuid == printer_guid && !WriteBackupTag(Nfc))
                Flux.Messages.LogMessage("Errore di aggiornamento tag", "Backup non effettuato", MessageLevel.ERROR, 38001);

            return nfc;

            Optional<bool> check_tag<TNFCTag2>(INFCReading<TNFCTag2> reading, ushort position) where TNFCTag2 : INFCTag
            {
                if (reading.CardId != nfc.CardId)
                    return true;

                if (position == Feeder.Position)
                    return true;

                if (!reading.Tag.HasValue || reading.Tag.Value.Loaded.HasValue || reading.Tag.Value.PrinterGuid != Guid.Empty)
                    return false;

                return default;
            }
        }

        public void StoreTag(Func<INFCTag, INFCTag> modify_tag) => StoreTag(t => (TNFCTag)modify_tag(t));
    }
}
