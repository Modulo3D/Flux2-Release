using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
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

        [RemoteContent(true, comparer:(nameof(IFluxOffsetViewModel.Position)))]
        public IObservableCache<IFluxOffsetViewModel, ushort> Offsets { get; }

        private readonly ObservableAsPropertyHelper<Optional<ToolId>> _GroupId;
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
                .ToPropertyRC(this, v => v.GroupId);

            Offsets = Flux.Feeders.Feeders.Connect()
                .AutoRefresh(f => f.FeederState)
                .Filter(f => f.FeederState != EFeederState.FEEDER_EMPTY)
                .Transform(f => (IFluxOffsetViewModel)new OffsetViewModel(this, f))
                .DisposeMany()
                .AsObservableCacheRC(this);

            var can_safe_start = Flux.StatusProvider
                 .WhenAnyValue(s => s.StatusEvaluation)
                 .Select(e => e.CanSafeCycle);

            var no_error_probe = Offsets.Connect()
                .TrueForAny(p => p.WhenAnyValue(o => o.ProbeState), s => s != FluxProbeState.ERROR_PROBE);

            var no_job = Flux.StatusProvider
                .WhenAnyValue(s => s.PrintingEvaluation)
                .Select(p => !p.FluxJob.HasValue);

            var can_probe = Observable.CombineLatest(
                can_safe_start,
                no_error_probe,
                no_job,
                (s, p, pp) => s && p && pp);

            var is_idle = Flux.StatusProvider
                .WhenAnyValue(e => e.StatusEvaluation)
                .Select(c => c.IsIdle);

            var user_settings = Flux.SettingsProvider.UserSettings;

            user_settings.Local
                .WhenAnyValue(v => v.GlobalZOffset)
                .BindToRC(this, v => v.GlobalZOffset, this);

            this.WhenAnyValue(v => v.GlobalZOffset)
                .BindToRC(user_settings, v => v.Local.GlobalZOffset, this);

            this.WhenAnyValue(v => v.GlobalZOffset)
                .Throttle(TimeSpan.FromSeconds(1))
                .SubscribeRC(_ => user_settings.PersistLocalSettings(), this);

            IncreaseGlobalZOffsetCommand = ReactiveCommandRC.Create(() => { ModifyOffset(o => o + 0.01f); }, this, is_idle);
            DecreaseGlobalZOffsetCommand = ReactiveCommandRC.Create(() => { ModifyOffset(o => o - 0.01f); }, this, is_idle);

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
                .SubscribeRC(_ => Flux.SettingsProvider.PersistLocalSettings(), this);

            ProbeOffsetsCommand = ReactiveCommandRC.CreateFromTask(async () =>
            {
                var tool_z_probe_unit = Flux.ConnectionProvider.GetArrayUnit(m => m.AXIS_PROBE, "tool_z");
                var has_tool_z_probe = Flux.ConnectionProvider.HasVariable(m => m.AXIS_PROBE, tool_z_probe_unit);
                if (has_tool_z_probe)
                {
                    await ProbeOffsetsAsync(false);
                }
                else
                {
                    await Flux.ConnectionProvider.CancelPrintAsync(true);

                    if (Flux.ConnectionProvider.HasVariable(c => c.Z_BED_HEIGHT))
                        await Flux.ConnectionProvider.WriteVariableAsync(c => c.Z_BED_HEIGHT, FluxViewModel.MaxZBedHeight);

                    if (Flux.ConnectionProvider.HasVariable(c => c.ENABLE_VACUUM))
                        await Flux.ConnectionProvider.WriteVariableAsync(c => c.ENABLE_VACUUM, true);

                    if (Flux.ConditionsProvider.ProbeCondition is ProbeConditionViewModel probe_condition)
                        probe_condition.IsProbed = false;

                    if (Flux.ConditionsProvider.FeelerGaugeCondition is FeelerGaugeConditionViewModel feeler_gauge_condition)
                        feeler_gauge_condition.Value = default;

                    Flux.Navigator.Navigate(ManualCalibration.Value);
                }
            }, this, can_probe);

            var offsets = Offsets.Connect()
                .ChangeKey(f => $"{f.Feeder.Position}")
                .Transform(f => (IRemoteControl)f)
                .AsObservableCacheRC(this);

            ManualCalibration = new Lazy<ManualCalibrationViewModel>(() => new ManualCalibrationViewModel(this));
        }

        private void ModifyOffset(Func<double, double> edit_func)
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
                    var result = await Flux.ShowDialogAsync(f => new ConfirmDialog(f, new RemoteText("toolCalibration", true), new RemoteText()));
                    if (result.result != DialogResult.Primary)
                        return;
                }
                else
                {
                    var result = await Flux.ShowDialogAsync(f => new ConfirmDialog(f, new RemoteText("toolCalibrationOverride", true), new RemoteText()));
                    if (result.result != DialogResult.Primary)
                        return;
                }
            }

            foreach (var offset in sorted_valid_offsets)
                await offset.ProbeOffsetAsync();

            await Flux.ConnectionProvider.CancelPrintAsync(true);
        }
    }
}
