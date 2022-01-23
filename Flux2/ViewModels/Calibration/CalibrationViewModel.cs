using DynamicData;
using DynamicData.Binding;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class CalibrationViewModel : FluxRoutableNavBarViewModel<CalibrationViewModel>, IFluxCalibrationViewModel
    {
        IFlux IFluxCalibrationViewModel.Flux => Flux;

        public IObservableList<IFluxOffsetViewModel> SortedOffsets { get; }
        [RemoteContent(true)]
        public IObservableCache<IFluxOffsetViewModel, ushort> Offsets { get; }

        private ObservableAsPropertyHelper<Optional<ToolId>> _GroupId;
        public Optional<ToolId> GroupId => _GroupId.Value;


        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ProbeOffsetsCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> IncreaseGlobalZOffsetCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> DecreaseGlobalZOffsetCommand { get; private set; }

        private Optional<double> _GlobalZOffset;
        [RemoteInput(0.01, converter: typeof(MillimeterConverter))]
        public Optional<double> GlobalZOffset
        {
            get => _GlobalZOffset;
            set => this.RaiseAndSetIfChanged(ref _GlobalZOffset, value);
        }

        public bool HasZProbe => Flux.ConnectionProvider.VariableStore.HasVariable(m => m.AXIS_PROBE, "tool_z");

        private ManualCalibrationViewModel ManualCalibration { get; set; }

        public CalibrationViewModel(FluxViewModel flux) : base(flux)
        {
            var tool_states = Flux.Feeders.Feeders.Connect()
                .AutoRefresh(f => f.ToolNozzle.State)
                .Transform(f => f.ToolNozzle.State, true)
                .QueryWhenChanged();

            _GroupId = Flux.Feeders.Feeders.Connect()
                .WatchOptional(tool_states.Select(ts =>
                {
                    foreach (var kvp in ts.KeyValues.OrderBy(kvp => kvp.Key))
                    {
                        if (!kvp.Value.IsLoaded())
                            continue;
                        return kvp.Key;
                    }
                    return Optional<ushort>.None;
                }))
                .ConvertMany(f => f.ToolNozzle.WhenAnyValue(n => n.Nfc).Select(nfc => (f.Position, nfc)))
                .Convert(f => 
                {
                    if (!f.nfc.CardId.HasValue)
                        return Optional<ToolId>.None;
                    if (!f.nfc.Tag.HasValue)
                        return Optional<ToolId>.None;

                    var tool_nfc = f.nfc.Tag.Value;
                    var tool_card = f.nfc.CardId.Value;
                    return new ToolId(f.Position, tool_card, tool_nfc);
                })
                .ToProperty(this, v => v.GroupId)
                .DisposeWith(Disposables);

            Offsets = Flux.Feeders.Feeders.Connect()
                .AutoRefresh(f => f.FeederState)
                .Filter(f => f.FeederState != EFeederState.FEEDER_EMPTY)
                .Transform(f => (IFluxOffsetViewModel)new OffsetViewModel(this, f))
                .DisposeMany()
                .AsObservableCache()
                .DisposeWith(Disposables);

            var comparer = SortExpressionComparer<IFluxOffsetViewModel>.Ascending(o =>
                o.Feeder.Position);

            SortedOffsets = Offsets.Connect()
                .RemoveKey()
                .Sort(comparer)
                .AsObservableList()
                .DisposeWith(Disposables);

            var can_safe_start = Flux.StatusProvider
             .WhenAnyValue(s => s.StatusEvaluation)
             .Select(e => e.CanSafeCycle);

            var no_error_probe = Offsets.Connect()
                .TrueForAll(p => p.WhenAnyValue(o => o.ProbeState), s => s != FluxProbeState.ERROR_PROBE);

            var can_probe = Observable.CombineLatest(
                can_safe_start,
                no_error_probe,
                (s, p) => s && p);

            var is_idle = Flux.StatusProvider
                .WhenAnyValue(e => e.StatusEvaluation)
                .Select(c => c.IsIdle.ValueOr(() => false));

            var user_settings = Flux.SettingsProvider.UserSettings;

            user_settings.Local
                .WhenAnyValue(v => v.GlobalZOffset)
                .BindTo(this, v => v.GlobalZOffset)
                .DisposeWith(Disposables);

            this.WhenAnyValue(v => v.GlobalZOffset)
                .BindTo(user_settings, v => v.Local.GlobalZOffset)
                .DisposeWith(Disposables);

            this.WhenAnyValue(v => v.GlobalZOffset)
                .Throttle(TimeSpan.FromSeconds(1))
                .Subscribe(_ => user_settings.PersistLocalSettings())
                .DisposeWith(Disposables);

            IncreaseGlobalZOffsetCommand = ReactiveCommand.Create(() => { ModifyOffset(o => o + 0.01f); }, is_idle)
                .DisposeWith(Disposables);
            DecreaseGlobalZOffsetCommand = ReactiveCommand.Create(() => { ModifyOffset(o => o - 0.01f); }, is_idle)
                .DisposeWith(Disposables);

            Observable.CombineLatest(
                IncreaseGlobalZOffsetCommand.IsExecuting,
                DecreaseGlobalZOffsetCommand.IsExecuting,
                (iz, dz) => (iz, dz))
                .PairWithPreviousValue()
                .Where(t =>
                {
                    return (t.OldValue.iz || t.OldValue.dz) &&
                        (!t.NewValue.iz && !t.NewValue.dz);
                })
                .Throttle(TimeSpan.FromSeconds(1))
                .Subscribe(_ => Flux.SettingsProvider.PersistLocalSettings())
                .DisposeWith(Disposables);

            ProbeOffsetsCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                if (HasZProbe)
                    await ProbeOffsetsAsync(false);
                else
                    Flux.Navigator.Navigate(ManualCalibration);
            }, can_probe)
                .DisposeWith(Disposables);

            var offsets = Offsets.Connect()
                .ChangeKey(f => $"{f.Feeder.Position}")
                .Transform(f => (IRemoteControl)f)
                .AsObservableCache()
                .DisposeWith(Disposables);

            ManualCalibration = new ManualCalibrationViewModel(this);
            ManualCalibration.InitializeRemoteView();
            ManualCalibration.DisposeWith(Disposables);
        }

        void ModifyOffset(Func<double, double> edit_func)
        {
            var old_offset = GlobalZOffset.ValueOr(() => 0.0);
            var new_offset = edit_func(old_offset);
            
            var settings = Flux.SettingsProvider.UserSettings.Local;
            settings.GlobalZOffset = new_offset;
        }

        public async Task ProbeOffsetsAsync(bool hard_probe)
        {
            if(!hard_probe)
            {
                if (Offsets.Items.Any(o => o.ProbeState == FluxProbeState.VALID_PROBE))
                {
                    var result = await Flux.ShowConfirmDialogAsync("CALIBRAZIONE UTENSILI", $"VUOI RICALIBRARE TUTTI GLI UTENSILI? {Environment.NewLine}LE CALIBRAZIONI PRECEDENTI VERRANNO PERSE.");
                    if (result != ContentDialogResult.Primary)
                        return;
                }
                else
                {
                    var result = await Flux.ShowConfirmDialogAsync("CALIBRAZIONE UTENSILI", "VUOI CALIBRARE TUTTI GLI UTENSILI?");
                    if (result != ContentDialogResult.Primary)
                        return;
                }
            }

            var temp_chamber = Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_CHAMBER);
            if (!await Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_CHAMBER, 40) ||
                !await WaitUtils.WaitForOptionalAsync(temp_chamber, t => t.Current >= t.Target, TimeSpan.FromMinutes(30)))
            {
                await Flux.ConnectionProvider.CancelPrintAsync(true);
                Flux.Messages.LogMessage("Errore di tastatura", "Camera ancora calda o operazione annullata", MessageLevel.ERROR, 0);
                return;
            }

            var has_plate = await Flux.ConnectionProvider.ReadVariableAsync(m => m.HAS_PLATE);
            if (has_plate.HasValue && has_plate.Value)
            {
                var temp_plate = Flux.ConnectionProvider.ObserveVariable(m => m.TEMP_PLATE);
                if (!await Flux.ConnectionProvider.WriteVariableAsync(m => m.TEMP_PLATE, 40) ||
                    !await WaitUtils.WaitForOptionalAsync(temp_plate, t => t.Current >= t.Target, TimeSpan.FromMinutes(30)))
                {
                    await Flux.ConnectionProvider.CancelPrintAsync(true);
                    Flux.Messages.LogMessage("Errore di tastatura", "Piatto ancora caldo o operazione annullata", MessageLevel.ERROR, 0);
                    return;
                }
            }

            foreach (var offset in SortedOffsets.Items)
                await offset.ProbeOffsetAsync();
 
            /*var offsets_by_temp = Offsets.Items
                .Where(o => o.Feeder.ToolMaterial.ExtrusionTemp.HasValue)
                .OrderBy(o => o.Feeder.ToolMaterial.ExtrusionTemp.Value);

            foreach (var offset_by_temp in offsets_by_temp)
            {
                var feeder = offset_by_temp.Feeder;
                if (!feeder.ToolMaterial.ExtrusionTemp.HasValue)
                    continue;

                var extrusion_temp = feeder.ToolMaterial.ExtrusionTemp.Value;
                await Flux.ConnectionProvider.SetExtruderTemperatureAsync(feeder.Position, extrusion_temp, false);
            }

            foreach (var offset_by_temp in offsets_by_temp)
            { 
                if (!await offset_by_temp.ProbeOffsetAsync())
                    break;
            }

            foreach (var offset_by_temp in offsets_by_temp)
            {
                var feeder = offset_by_temp.Feeder;
                await Flux.ConnectionProvider.SetExtruderTemperatureAsync(feeder.Position, 0.0, false);
            }*/
        }

    }

    public class LayerHeightComparer : IEqualityComparer<double>
    {
        public LayerHeightComparer()
        {
        }
        public bool Equals(double x, double y)
        {
            return Math.Round(x, 2) == Math.Round(y, 2);
        }
        public int GetHashCode(double d)
        {
            return Math.Round(d, 2).GetHashCode();
        }
    }
}
