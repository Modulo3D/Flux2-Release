namespace Flux.ViewModels
{
    /*public abstract class ToolChangeViewModel : FeederOperationViewModel<ToolChangeViewModel>
    {
        private ObservableAsPropertyHelper<bool> _ToolConnected;
        public bool ToolConnected 
        {
            get
            {
                if (_ToolConnected == default)
                {
                    _ToolConnected = Feeder.ToolNozzle
                        .WhenAnyValue(v => v.Temperature)
                        .ConvertOr(t => t.Current < 1000, () => true)
                        .ToProperty(this, v => v.ToolConnected);
                }
                return _ToolConnected.Value;
            }
        }

        public ReactiveCommand<Unit, Unit> ToggleClampCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenTopLockCommand { get; }
        public ReactiveCommand<Unit, Unit> PreheatCommand { get; set; }

        public abstract bool CanToggleClamp { get; }
        public abstract bool CanOpenTopLock { get; }
        public abstract bool CanPreheat { get; }

        public ToolChangeViewModel(FeederViewModel feeder) : base(feeder)
        {
            var is_idle = Flux.StatusProvider.IsIdle;
            var can_toggle_clamp = Observable.CombineLatest(is_idle, Observable.Return(CanToggleClamp), (i, c) => i && c);
            var can_open_lock = Observable.CombineLatest(is_idle, Observable.Return(CanOpenTopLock), (i, c) => i && c);
            var can_preheat = Observable.CombineLatest(is_idle, Observable.Return(CanPreheat), (i, c) => i && c);

            PreheatCommand = ReactiveCommand.CreateFromTask(PreheatAsync, can_preheat);
            ToggleClampCommand = ReactiveCommand.CreateFromTask(async () => { await Flux.ConnectionProvider.ToggleVariableAsync(m => m.OPEN_HEAD_CLAMP); }, can_toggle_clamp);
            OpenTopLockCommand = ReactiveCommand.CreateFromTask(async () => { await Flux.ConnectionProvider.ToggleVariableAsync(m => m.OPEN_LOCK, "top"); }, can_open_lock);
        }

        private async Task PreheatAsync()
        {
            var temp_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.TEMP_TOOL, Feeder.Position);
            if (!temp_key.HasValue)
                return;

            if (Feeder.ToolNozzle.Temperature.ConvertOr(t => t.Target == 0, () => false))
            {
                var temperatures = new[] { 300.0, 350.0, 400.0 };
                var (result, values) = await Utils.ShowSelectionAsync(Flux, "SELEZIONARE UNA TEMPERATURA", () => new[]
                {
                    ComboOption.Create("temp", "TEMPERATURE DISPONIBILI", temperatures, converter: typeof(TemperatureConverter)),
                });

                if (result == ContentDialogResult.Secondary)
                    return;

                var temperature = values[0].Convert(m => (double)m);
                if (!temperature.HasValue)
                    return;

                await Flux.ConnectionProvider.WriteVariableAsync(m => m.AUTO_FAN, false);
                await Flux.ConnectionProvider.WriteVariableAsync(m => m.ENABLE_HEAD_FAN, false);
                await Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_TOOL, temp_key.Value, temperature.Value);
            }
            else
            {
                await Flux.ConnectionProvider.WriteVariableAsync(m => m.AUTO_FAN, true);
                await Flux.ConnectionProvider.WriteVariableAsync(m => m.ENABLE_HEAD_FAN, true);
                await Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_TOOL, temp_key.Value, 0);
            }
        }

        protected override async Task<bool> CancelOperationAsync()
        {
            var temp_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.TEMP_TOOL, Feeder.Position);
            if (!temp_key.HasValue)
                return false;

            if (!await Flux.ConnectionProvider.WriteVariableAsync(m => m.AUTO_FAN, true))
                return false;

            if (!await Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_TOOL, temp_key.Value, 0))
                return false;

            if (!await Flux.ConnectionProvider.ResetAsync())
                return false;

            Feeder.ToolNozzle.InMaintenance = false;

            return Flux.Navigator.NavigateHome();
        }

        public override Task UpdateNFCAsync()
        {
            throw new NotImplementedException();
        }

        protected override IObservable<bool> FindCanUpdateNFC()
        {
            return Observable.Return(false);
        }
    }

    public enum ToolProceduresStart : uint
    {
        CHANGE_TOOL = 0,
        CHANGE_NOZZLE = 1,
    }

    public enum ToolProceduresEnd : uint
    {
        NONE = 0,
        DELETE_ALL = 1,
        DELETE_PROBE = 2
    }

    public class StartToolChangeViewModel : ToolChangeViewModel
    {
        public override bool CanToggleClamp => false;
        public override bool CanOpenTopLock => false;
        public override bool CanPreheat => false;

        public StartToolChangeViewModel(FeederViewModel feeder) : base(feeder)
        { 
        }

        protected override IObservable<bool> CanCancelOperation()
        {
            return Flux.StatusProvider.CanSafeStop;
        }
        protected override IObservable<bool> CanExecuteOperation()
        {
            return Observable.CombineLatest(
                this.WhenAnyValue(f => f.AllConditionsTrue),
                Flux.StatusProvider.CanSafeStart,
                (c, s) => c && s);
        }

        protected override string FindTitleText(bool idle)
        {
            return idle ? "PRONTO ALLA SOSTITUZIONE" : "PREPARAZIONE IN CORSO...";
        }
        protected override string FindOperationText(bool idle)
        {
            return idle ? "PREPARA SOSTITUZIONE" : "---";
        }
        protected override async Task<bool> ExecuteOperationAsync()
        {
            Feeder.ToolNozzle.InMaintenance = true;

            var tool_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.TEMP_TOOL, Feeder.Position);
            if (!tool_key.HasValue)
                return false;

            if (!await Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_TOOL, tool_key.Value, 0))
                return false;

            var (result, values) = await Utils.ShowSelectionAsync(Flux, "SELEZIONARE UNA PROCEDURA", () => new[]
            {
                ComboOption.Create("procedure", "PROCEDURE DISPONIBILI", Enum.GetValues<ToolProceduresStart>(), p => (uint)p),
            });

            if (result == ContentDialogResult.Secondary)
                return false;

            var procedure = values[0].Convert(m => (ToolProceduresStart)m);
            if (!procedure.HasValue)
                return false;

            switch (procedure.Value)
            {
                case ToolProceduresStart.CHANGE_TOOL:

                    if (!await Flux.ConnectionProvider.MoveToReaderAsync(Feeder.Position, async () =>
                    {
                        var operator_usb = Flux.StorageProvider.OperatorUSB;
                        var reading = await Feeder.ToolNozzle.ReadTagAsync(false, operator_usb.ConvertOr(o => o.RewriteNFC, () => false));
                        if (!reading.HasValue)
                            return false;
                        await Feeder.ToolNozzle.StoreTagAsync(reading.Value);
                        return await Feeder.ToolNozzle.UnlockTagAsync();
                    }))
                        return false;

                    if (!await Flux.ConnectionProvider.GotoPurgePositionAsync(Feeder.Position))
                        return false;

                    return Flux.Navigator.Navigate(new InToolChangeViewModel(Feeder));
             
                case ToolProceduresStart.CHANGE_NOZZLE:

                    Feeder.ToolNozzle.StoreTag(tn => tn.SetNozzle(default));

                    if (!await Flux.ConnectionProvider.MoveToReaderAsync(Feeder.Position, async () =>
                    {
                        var operator_usb = Flux.StorageProvider.OperatorUSB;
                        var reading = await Feeder.ToolNozzle.ReadTagAsync(false, operator_usb.ConvertOr(o => o.RewriteNFC, () => false));
                        if (!reading.HasValue)
                            return false;
                        await Feeder.ToolNozzle.StoreTagAsync(reading.Value);
                        return await Feeder.ToolNozzle.UnlockTagAsync();
                    }))
                        return false;

                    if (!await Flux.ConnectionProvider.GotoPurgePositionAsync(Feeder.Position))
                        return false;

                    return Flux.Navigator.Navigate(new EndToolChangeViewModel(Feeder));
            }

            return false;
        }
        protected override IEnumerable<IConditionViewModel> FindConditions()
        {
            yield return Flux.StatusProvider.TopLockClosed;
            yield return Flux.StatusProvider.ChamberLockClosed;
        }
        protected override IObservable<string> FindUpdateNFCText()
        {
            throw new NotImplementedException();
        }
    }

    public class InToolChangeViewModel : ToolChangeViewModel
    {
        public override bool CanToggleClamp => true;
        public override bool CanOpenTopLock => true;
        public override bool CanPreheat => false;

        public InToolChangeViewModel(FeederViewModel feeder) : base(feeder)
        {
        }

        protected override IObservable<bool> CanCancelOperation()
        {
            return this.WhenAnyValue(f => f.AllConditionsFalse);
        }
        protected override IObservable<bool> CanExecuteOperation()
        {
            return this.WhenAnyValue(f => f.AllConditionsTrue);
        }

        protected override string FindTitleText(bool idle)
        {
            return "RIMUOVI L'UTENSILE";
        }
        protected override string FindOperationText(bool idle)
        {
            return "UTENSILE RIMOSSO";
        }
        protected override Task<bool> ExecuteOperationAsync()
        {
            Feeder.ToolNozzle.InMaintenance = true;
            return Task.FromResult(Flux.Navigator.Navigate(new EndToolChangeViewModel(Feeder)));
        }
        protected override IEnumerable<IConditionViewModel> FindConditions()
        {
            yield return Flux.StatusProvider.TopLockOpen;
            
            yield return ConditionViewModel.Create("toolDisconnected",
                this.WhenAnyValue(v => v.ToolConnected).Select(c => Optional.Some(!c)),
                (value, valid) => valid ? "UTENSILE SCOLLEGATO" : "SCOLLEGA L'UTENSILE");

            yield return Flux.StatusProvider.ClampOpen;
        }
        protected override IObservable<string> FindUpdateNFCText()
        {
            throw new NotImplementedException();
        }
    }

    public class EndToolChangeViewModel : ToolChangeViewModel
    {
        public override bool CanToggleClamp => true;
        public override bool CanOpenTopLock => true;
        public override bool CanPreheat => true;

        public EndToolChangeViewModel(FeederViewModel feeder) : base(feeder)
        {
        }

        protected override IObservable<bool> CanCancelOperation()
        {
            return Flux.StatusProvider.CanSafeStop;
        }
        protected override IObservable<bool> CanExecuteOperation()
        {
            return Observable.CombineLatest(
                this.WhenAnyValue(f => f.AllConditionsTrue),
                Flux.StatusProvider.CanSafeStart,
                (c, s) => c && s);
        }

        protected override string FindTitleText(bool idle)
        {
            return idle ? "INSERISCI L'UTENSILE" : "LETTURA UTENSILE...";
        }
        protected override string FindOperationText(bool idle)
        {
            return idle ? "TERMINA SOSTITUZIONE" : "---";
        }
        protected override async Task<bool> ExecuteOperationAsync()
        {
            Feeder.ToolNozzle.InMaintenance = true;

            if (!await Flux.ConnectionProvider.WriteVariableAsync(m => m.AUTO_FAN, true))
                return false;

            var tool_key = Flux.ConnectionProvider.VariableStore.GetArrayUnit(m => m.TEMP_TOOL, Feeder.Position);
            if (!tool_key.HasValue)
                return false;

            if (!await Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_TOOL, tool_key.Value, 0))
                return false;

            if (!await Flux.ConnectionProvider.MoveToReaderAsync(Feeder.Position, async () =>
            {
                var operator_usb = Flux.StorageProvider.OperatorUSB;
                var reading = await Feeder.ToolNozzle.ReadTagAsync(true, operator_usb.ConvertOr(o => o.RewriteNFC, () => false));
                if (reading.HasValue)
                    return false;    

                var nfc = await Feeder.ToolNozzle.StoreTagAsync(reading.Value);
                if (!nfc.Tag.HasValue)
                    return false;

                if (nfc.Tag.Value.NozzleGuid == Guid.Empty)
                {
                    var card_id = nfc.CardId;
                    if (!card_id.HasValue)
                        return false;

                    var tool = Feeder.ToolNozzle.Document.tool;
                    if (!tool.HasValue)
                        return false;
                        
                    var nozzle = await Feeder.ToolNozzle.FindNewNozzleAsync(card_id.Value, tool.Value, false);
                    if (!nozzle.result)
                        return false;

                    Feeder.ToolNozzle.StoreTag(tn =>
                    {
                        tn = tn.SetNozzle(nozzle.nozzle);
                        tn = tn.SetMaxWeight(nozzle.max_weight);
                        return tn.SetLoaded(Feeder.Position);
                    });
                }
                
                return true;
            }))
            return false;

            var (result, values) = await Utils.ShowSelectionAsync(Flux, "SELEZIONARE UNA PROCEDURA", () => new[]
            {
                ComboOption.Create("procedure", "PROCEDURE DISPONIBILI", Enum.GetValues<ToolProceduresEnd>(), p => (uint)p),
            });

            if (result == ContentDialogResult.Secondary)
                return false;

            var procedure = values[0].Convert(m => (ToolProceduresEnd)m);
            if (!procedure.HasValue)
                return false;

            var settings = Flux.SettingsProvider.UserSettings.Local;
            switch (procedure.Value)
            {
                case ToolProceduresEnd.NONE:
                    break;
                case ToolProceduresEnd.DELETE_ALL:

                    var probe_offsets = new List<ProbeOffset>(settings.ProbeOffsets.Items);
                    foreach (var probe_offset in probe_offsets)
                    {
                        if(probe_offset.Key.ToolCardId != Feeder.ToolNozzle.Nfc.CardId)
                            continue;
                        
                        if (settings.ProbeOffsets.Lookup(probe_offset.Key).HasValue)
                            settings.ProbeOffsets.Remove(probe_offset);
                    }

                    var user_offsets = new List<UserOffset>(settings.UserOffsets.Items);
                    foreach (var user_offset in user_offsets)
                    {
                        if (user_offset.Key.GroupPosition != Feeder.Position ||
                            user_offset.Key.GroupCardId != Feeder.ToolNozzle.Nfc.CardId)
                        { 
                            if (user_offset.Key.RelativePosition != Feeder.Position ||
                                user_offset.Key.RelativeCardId != Feeder.ToolNozzle.Nfc.CardId)
                                continue;
                        }

                        if(settings.UserOffsets.Lookup(user_offset.Key).HasValue)
                            settings.UserOffsets.Remove(user_offset);
                    }

                    break;

                case ToolProceduresEnd.DELETE_PROBE:

                    probe_offsets = new List<ProbeOffset>(settings.ProbeOffsets.Items);
                    foreach (var probe_offset in probe_offsets)
                    {
                        if (probe_offset.Key.ToolCardId != Feeder.ToolNozzle.Nfc.CardId)
                            continue;

                        if (settings.ProbeOffsets.Lookup(probe_offset.Key).HasValue)
                            settings.ProbeOffsets.Remove(probe_offset);
                    }

                    break;
            }

            Feeder.ToolNozzle.InMaintenance = false;
            return Flux.Navigator.NavigateHome();
        }
        protected override IEnumerable<IConditionViewModel> FindConditions()
        {
            yield return Flux.StatusProvider.ChamberLockClosed;
            
            yield return ConditionViewModel.Create("toolConnected",
                this.WhenAnyValue(v => v.ToolConnected).Select(c => Optional.Some(c)),
                (value, valid) => valid ? "UTENSILE RICOLLEGATO" : "RICOLLEGA L'UTENSILE");

            yield return ConditionViewModel.Create("toolInMagazine",
                Feeder.ToolNozzle.WhenAnyValue(v => v.State).Select(c => Optional.Some(c.IsInMagazine())),
                (value, valid) => valid ? "UTENSILE RIPOSTO" : "RIPONI L'UTENSILE");

            yield return Flux.StatusProvider.ClampClosed;

            yield return Flux.StatusProvider.TopLockClosed;
        }
        protected override IObservable<string> FindUpdateNFCText()
        {
            throw new NotImplementedException();
        }
    }*/
}
