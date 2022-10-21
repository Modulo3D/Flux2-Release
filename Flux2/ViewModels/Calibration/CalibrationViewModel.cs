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

        [RemoteContent(true)]
        public IObservableCache<IFluxOffsetViewModel, ushort> Offsets { get; }

        private ObservableAsPropertyHelper<Optional<ToolId>> _GroupId;
        public Optional<ToolId> GroupId => _GroupId.Value;


        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ProbeOffsetsCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> IncreaseGlobalZOffsetCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> DecreaseGlobalZOffsetCommand { get; private set; }

        private Optional<double> _GlobalZOffset;
        [RemoteInput(step: 0.01, converter: typeof(MillimeterConverter))]
        public Optional<double> GlobalZOffset
        {
            get => _GlobalZOffset;
            set => this.RaiseAndSetIfChanged(ref _GlobalZOffset, value);
        }

        private Lazy<ManualCalibrationViewModel> ManualCalibration { get; set; }

        public CalibrationViewModel(FluxViewModel flux) : base(flux)
        {
            var tool_states = Flux.Feeders.Feeders.Connect()
                .AutoRefresh(f => f.ToolNozzle.State)
                .Transform(f => f.ToolNozzle.State, true)
                .QueryWhenChanged();

            var group_tool = tool_states.Select(ts =>
                {
                    foreach (var kvp in ts.KeyValues.OrderBy(kvp => kvp.Key))
                    {
                        if (!kvp.Value.IsLoaded())
                            continue;
                        return kvp.Key;
                    }
                    return Optional<ushort>.None;
                })
                .Throttle(TimeSpan.FromSeconds(1));

            _GroupId = Flux.Feeders.Feeders.Connect()
                .WatchOptional(group_tool)
                .ConvertMany(f =>
                {
                    return f.ToolNozzle.NFCSlot.WhenAnyValue(n => n.Nfc)
                        .Select(nfc => (f.Position, nfc));
                })
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
                .DisposeWith(Disposables);;

            var can_safe_start = Flux.StatusProvider
                 .WhenAnyValue(s => s.StatusEvaluation)
                 .Select(e => e.CanSafeCycle);

            var no_error_probe = Offsets.Connect()
                .TrueForAny(p => p.WhenAnyValue(o => o.ProbeState), s => s != FluxProbeState.ERROR_PROBE);

            var can_probe = Observable.CombineLatest(
                can_safe_start,
                no_error_probe,
                (s, p) => s && p);

            var is_idle = Flux.StatusProvider
                .WhenAnyValue(e => e.StatusEvaluation)
                .Select(c => c.IsIdle);

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
                var tool_z_probe_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.AXIS_PROBE, "tool_z");
                var has_tool_z_probe = Flux.ConnectionProvider.HasVariable(m => m.AXIS_PROBE, tool_z_probe_unit);
                if (has_tool_z_probe)
                {
                    await ProbeOffsetsAsync(false);
                }
                else
                {
                    if (Flux.ConnectionProvider.HasVariable(c => c.Z_BED_HEIGHT))
                        await Flux.ConnectionProvider.WriteVariableAsync(c => c.Z_BED_HEIGHT, FluxViewModel.MaxZBedHeight);

                    if (Flux.ConnectionProvider.HasVariable(c => c.ENABLE_VACUUM))
                        await Flux.ConnectionProvider.WriteVariableAsync(c => c.ENABLE_VACUUM, true);

                    Flux.Navigator.Navigate(ManualCalibration.Value);
                }
            }, can_probe)
                .DisposeWith(Disposables);

            var offsets = Offsets.Connect()
                .ChangeKey(f => $"{f.Feeder.Position}")
                .Transform(f => (IRemoteControl)f)
                .AsObservableCache()
                .DisposeWith(Disposables);

            ManualCalibration = new Lazy<ManualCalibrationViewModel>(() => new ManualCalibrationViewModel(this));
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
            var sorted_valid_offsets = Offsets.Items
                .OrderBy(o => o.Feeder.Position)
                .Where(o => o.ProbeState != FluxProbeState.ERROR_PROBE);
            
            if (!hard_probe)
            {
                if (sorted_valid_offsets.Any(o => o.ProbeState == FluxProbeState.VALID_PROBE))
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

            foreach (var offset in sorted_valid_offsets)
                await offset.ProbeOffsetAsync();
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
