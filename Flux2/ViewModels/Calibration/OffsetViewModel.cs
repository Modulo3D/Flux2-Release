﻿using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class OffsetViewModel : RemoteControl<OffsetViewModel>, IFluxOffsetViewModel
    {
        public FluxViewModel Flux { get; }
        public IFluxFeederViewModel Feeder { get; }
        public CalibrationViewModel Calibration { get; }

        private readonly ObservableAsPropertyHelper<bool> _DebugOffsets;
        [RemoteOutput(true)]
        public bool DebugOffsets => _DebugOffsets.Value;

        private readonly ObservableAsPropertyHelper<Optional<ToolId>> _ToolId;
        public Optional<ToolId> ToolId => _ToolId.Value;

        private readonly ObservableAsPropertyHelper<Optional<UserOffsetKey>> _UserOffsetKey;
        [RemoteOutput(true, typeof(HasValueConverter<UserOffsetKey>))]
        public Optional<UserOffsetKey> UserOffsetKey => _UserOffsetKey.Value;

        private readonly ObservableAsPropertyHelper<Optional<ProbeOffsetKey>> _ProbeOffsetKey;
        [RemoteOutput(true, typeof(HasValueConverter<ProbeOffsetKey>))]
        public Optional<ProbeOffsetKey> ProbeOffsetKey => _ProbeOffsetKey.Value;

        private readonly ObservableAsPropertyHelper<Optional<ToolOffset>> _ToolOffset;
        public Optional<ToolOffset> ToolOffset => _ToolOffset.Value;

        private readonly ObservableAsPropertyHelper<Optional<UserOffset>> _UserOffset;
        public Optional<UserOffset> UserOffset => _UserOffset.Value;

        private readonly ObservableAsPropertyHelper<Optional<ProbeOffset>> _ProbeOffset;
        public Optional<ProbeOffset> ProbeOffset => _ProbeOffset.Value;

        private readonly ObservableAsPropertyHelper<Optional<FluxOffset>> _FluxOffset;
        public Optional<FluxOffset> FluxOffset => _FluxOffset.Value;

        private readonly ObservableAsPropertyHelper<bool> _IsOffsetRoot;
        [RemoteOutput(true)]
        public bool IsOffsetRoot => _IsOffsetRoot.Value;

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> SetProbeOffsetCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ResetProbeOffsetCommand { get; }

        private double _XUserOffset;
        [RemoteInput(step: 0.05, converter: typeof(MillimeterConverter))]
        public double XUserOffset
        {
            get => _XUserOffset;
            set => this.RaiseAndSetIfChanged(ref _XUserOffset, value);
        }

        private double _YUserOffset;
        [RemoteInput(step: 0.05, converter: typeof(MillimeterConverter))]
        public double YUserOffset
        {
            get => _YUserOffset;
            set => this.RaiseAndSetIfChanged(ref _YUserOffset, value);
        }

        private double _ZUserOffset;
        [RemoteInput(step: 0.01, converter: typeof(MillimeterConverter))]
        public double ZUserOffset
        {
            get => _ZUserOffset;
            set => this.RaiseAndSetIfChanged(ref _ZUserOffset, value);
        }

        private readonly ObservableAsPropertyHelper<double> _XProbeOffset;
        [RemoteOutput(true, converter: typeof(MillimeterConverter))]
        public double XProbeOffset => _XProbeOffset.Value;

        private readonly ObservableAsPropertyHelper<double> _YProbeOffset;
        [RemoteOutput(true, converter: typeof(MillimeterConverter))]
        public double YProbeOffset => _YProbeOffset.Value;

        private readonly ObservableAsPropertyHelper<double> _ZProbeOffset;
        [RemoteOutput(true, converter: typeof(MillimeterConverter))]
        public double ZProbeOffset => _ZProbeOffset.Value;

        private readonly ObservableAsPropertyHelper<FluxProbeState> _ProbeState;
        [RemoteOutput(true)]
        public FluxProbeState ProbeState => _ProbeState.Value;

        private readonly ObservableAsPropertyHelper<string> _ProbeStateBrush;
        [RemoteOutput(true)]
        public string ProbeStateBrush => _ProbeStateBrush.Value;

        [RemoteOutput(false)]
        public ushort Position => Feeder.Position;

        public OffsetViewModel(CalibrationViewModel calibration, IFluxFeederViewModel feeder) : base($"{typeof(OffsetViewModel).GetRemoteControlName()}??{feeder.Position}")
        {
            Feeder = feeder;
            Flux = calibration.Flux;
            Calibration = calibration;

            var user_settings = Flux.SettingsProvider.UserSettings;

            _ToolId = Feeder.ToolNozzle.NFCSlot
                .WhenAnyValue(v => v.Nfc)
                .Select(nfc =>
                {
                    if (!nfc.CardId.HasValue)
                        return Optional<ToolId>.None;
                    if (!nfc.Tag.HasValue)
                        return Optional<ToolId>.None;
                    var tool_nfc = nfc.Tag.Value;
                    var tool_card = nfc.CardId.Value;
                    return new ToolId(Feeder.Position, tool_card, tool_nfc);
                })
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.ToolId);

            _ToolOffset = Observable.CombineLatest(
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                Feeder.ToolNozzle.NFCSlot.WhenAnyValue(v => v.Nfc),
                GetToolOffset)
                .SelectAsync()
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.ToolOffset);

            _UserOffsetKey = Observable.CombineLatest(
                Calibration.WhenAnyValue(v => v.GroupId),
                Feeder.ToolNozzle.NFCSlot.WhenAnyValue(v => v.Nfc),
                GetUserOffsetKey)
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.UserOffsetKey);

            var material = Feeder.WhenAnyValue(m => m.SelectedMaterial);

            _ProbeOffsetKey = Observable.CombineLatest(
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                Feeder.ToolNozzle.NFCSlot.WhenAnyValue(v => v.Nfc),
                material.ConvertMany(m => m.NFCSlot.WhenAnyValue(v => v.Nfc)),
                GetProbeOffsetKey)
                .SelectAsync()
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.ProbeOffsetKey);

            _UserOffset = user_settings.Local.UserOffsets.Connect()
                .WatchOptional(this.WhenAnyValue(v => v.UserOffsetKey))
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.UserOffset);

            _ProbeOffset = user_settings.Local.ProbeOffsets.Connect()
                .WatchOptional(this.WhenAnyValue(v => v.ProbeOffsetKey))
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.ProbeOffset);

            _ProbeState = Observable.CombineLatest(
                this.WhenAnyValue(s => s.ToolOffset),
                this.WhenAnyValue(s => s.ProbeOffset),
                Feeder.ToolNozzle.WhenAnyValue(f => f.State),
                material.ConvertMany(m => m.WhenAnyValue(f => f.State)),
                GetProbeState)
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.ProbeState);

            _IsOffsetRoot = this.WhenAnyValue(v => v.UserOffsetKey)
                .Convert(o => o.RelativeTool.Position == o.GroupTool.Position)
                .ValueOr(() => false)
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.IsOffsetRoot);

            Observable.CombineLatest(
                this.WhenAnyValue(v => v.ProbeOffset),
                this.WhenAnyValue(v => v.UserOffset),
                (_, _) => Unit.Default)
                .Throttle(TimeSpan.FromSeconds(1))
                .SubscribeRC(_ => user_settings.PersistLocalSettings(), this);

            var has_probe_offset_key = Observable.CombineLatest(
                Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.IsIdle),
                this.WhenAnyValue(v => v.ProbeOffsetKey),
                (i, p) => i && p.HasValue);

            ResetProbeOffsetCommand = ReactiveCommandRC.Create(ResetProbeOffset, this, has_probe_offset_key);

            SetProbeOffsetCommand = ReactiveCommandRC.CreateFromTask(SetProbeOffsetAsync, this, has_probe_offset_key);

            var userOffset = this.WhenAnyValue(v => v.UserOffset);
            var probeOffset = this.WhenAnyValue(v => v.ProbeOffset);

            userOffset
                .ConvertOr(o => o.X, () => 0)
                .DistinctUntilChanged()
                .BindToRC(this, v => v.XUserOffset);
            this.WhenAnyValue(v => v.XUserOffset)
                .DistinctUntilChanged()
                .SubscribeRC(x => ModifyUserOffset(o => new UserOffset(o.Key, x, o.Y, o.Z)), this);

            userOffset
                .ConvertOr(o => o.Y, () => 0)
                .BindToRC(this, v => v.YUserOffset);
            this.WhenAnyValue(v => v.YUserOffset)
                .SubscribeRC(y => ModifyUserOffset(o => new UserOffset(o.Key, o.X, y, o.Z)), this);

            userOffset.ConvertOr(o => o.Z, () => 0)
              .BindToRC(this, v => v.ZUserOffset);
            this.WhenAnyValue(v => v.ZUserOffset)
                .SubscribeRC(z => ModifyUserOffset(o => new UserOffset(o.Key, o.X, o.Y, z)), this);

            _XProbeOffset = probeOffset
                .ConvertOr(o => o.X, () => 0)
                .ToPropertyRC(this, v => v.XProbeOffset);

            _YProbeOffset = probeOffset
                .ConvertOr(o => o.Y, () => 0)
                .ToPropertyRC(this, v => v.YProbeOffset);

            _ZProbeOffset = probeOffset
                .ConvertOr(o => o.Z, () => 0)
                .ToPropertyRC(this, v => v.ZProbeOffset);

            _ProbeStateBrush = this.WhenAnyValue(v => v.ProbeState)
                .Select(state => state switch
                {
                    FluxProbeState.INVALID_PROBE => FluxColors.Warning,
                    FluxProbeState.VALID_PROBE => FluxColors.Selected,
                    FluxProbeState.NO_PROBE => FluxColors.Inactive,
                    _ => FluxColors.Error,
                })
                .ToPropertyRC(this, v => v.ProbeStateBrush);

            _FluxOffset = Observable.CombineLatest(
                Calibration.WhenAnyValue(v => v.GlobalZOffset),
                this.WhenAnyValue(v => v.ProbeOffset),
                this.WhenAnyValue(v => v.UserOffset),
                GetOffset)
                .DistinctUntilChanged()
                .ToPropertyRC(this, v => v.FluxOffset);

            this.WhenAnyValue(v => v.FluxOffset)
                .Where(o => o.HasValue)
                .Throttle(TimeSpan.FromSeconds(1))
                .SubscribeRC(async offset => await Flux.ConnectionProvider.SetToolOffsetsAsync(offset.Value), this);

            _DebugOffsets = Flux.MCodes
                .WhenAnyValue(s => s.OperatorUSB)
                .ConvertOr(usb => usb.AdvancedSettings, () => false)
                .ToPropertyRC(this, v => v.DebugOffsets);
        }

        private static FluxProbeState GetProbeState(Optional<ToolOffset> tool_offset, Optional<ProbeOffset> probe_offset, ToolNozzleState tool_state, Optional<MaterialState> material_state)
        {
            if (!tool_offset.HasValue)
                return FluxProbeState.ERROR_PROBE;

            if (probe_offset.HasValue && Math.Abs(probe_offset.Value.X - tool_offset.Value.X) > 5)
                return FluxProbeState.ERROR_PROBE;
            if (probe_offset.HasValue && Math.Abs(probe_offset.Value.Y - tool_offset.Value.Y) > 5)
                return FluxProbeState.ERROR_PROBE;
            if (probe_offset.HasValue && tool_offset.Value.Z - probe_offset.Value.Z > 5)
                return FluxProbeState.ERROR_PROBE;

            if (!tool_state.IsLoaded() ||
                !material_state.HasValue ||
                !material_state.Value.IsLoaded())
                return FluxProbeState.NO_PROBE;

            if (!probe_offset.HasValue ||
                probe_offset.Value.Z >= tool_offset.Value.Z)
                return FluxProbeState.INVALID_PROBE;

            return FluxProbeState.VALID_PROBE;
        }

        private async Task<Optional<ProbeOffsetKey>> GetProbeOffsetKey(Optional<ILocalDatabase> db, NFCReading<NFCToolNozzle> tool_reading, Optional<NFCReading<NFCMaterial>> material_reading)
        {
            var user_settings = Flux.SettingsProvider.UserSettings;

            if (!tool_reading.CardId.HasValue)
                return Optional<ProbeOffsetKey>.None;
            if (!tool_reading.Tag.HasValue)
                return Optional<ProbeOffsetKey>.None;
            if (!material_reading.HasValue)
                return Optional<ProbeOffsetKey>.None;
            if (!material_reading.Value.Tag.HasValue)
                return Optional<ProbeOffsetKey>.None;

            var tool_nfc = tool_reading.Tag.Value;
            var tool_card = tool_reading.CardId.Value;
            var relative_id = new ToolId(Feeder.Position, tool_card, tool_nfc);

            var probe_offset_key = new ProbeOffsetKey(relative_id, material_reading.Value.Tag.Value.MaterialGuid);
            var probe_offset_lookup = user_settings.Local.ProbeOffsets.Lookup(probe_offset_key);
            if (!probe_offset_lookup.HasValue)
            {
                if (db.HasValue)
                {
                    var tool = await tool_nfc.GetDocumentAsync<Tool>(db.Value, tn => tn.ToolGuid);
                    if (tool.HasValue)
                    {
                        var x = tool.Value[n => n.ToolXOffset, 0.0];
                        var y = tool.Value[n => n.ToolYOffset, 0.0];
                        var z = tool.Value[n => n.ToolZOffset, 0.0];
                        var probe_offset = new ProbeOffset(probe_offset_key, x, y, z);
                        user_settings.Local.ProbeOffsets.AddOrUpdate(probe_offset);
                    }
                }
            }
            return probe_offset_key;
        }

        private Optional<UserOffsetKey> GetUserOffsetKey(Optional<ToolId> group_id, NFCReading<NFCToolNozzle> tool_reading)
        {
            var user_settings = Flux.SettingsProvider.UserSettings;

            if (!group_id.HasValue)
                return Optional<UserOffsetKey>.None;
            if (!tool_reading.CardId.HasValue)
                return Optional<UserOffsetKey>.None;
            if (!tool_reading.Tag.HasValue)
                return Optional<UserOffsetKey>.None;

            var tool_nfc = tool_reading.Tag.Value;
            var tool_card = tool_reading.CardId.Value;
            var relative_id = new ToolId(Feeder.Position, tool_card, tool_nfc);

            var user_offset_key = new UserOffsetKey(group_id.Value, relative_id);
            var user_offset_lookup = user_settings.Local.UserOffsets.Lookup(user_offset_key);
            if (!user_offset_lookup.HasValue)
            {
                var user_offset = new UserOffset(user_offset_key, 0, 0, 0);
                user_settings.Local.UserOffsets.AddOrUpdate(user_offset);
            }
            return user_offset_key;
        }

        private static async Task<Optional<ToolOffset>> GetToolOffset(Optional<ILocalDatabase> db, NFCReading<NFCToolNozzle> nfc)
        {
            if (!db.HasValue)
                return Optional<ToolOffset>.None;
            if (!nfc.Tag.HasValue)
                return Optional<ToolOffset>.None;
            var tool = await nfc.Tag.Value.GetDocumentAsync<Tool>(db.Value, tn => tn.ToolGuid);
            if (!tool.HasValue)
                return Optional<ToolOffset>.None;
            var x = tool.Value[t => t.ToolXOffset, 0.0];
            var y = tool.Value[t => t.ToolYOffset, 0.0];
            var z = tool.Value[t => t.ToolZOffset, 0.0];
            return new ToolOffset(x, y, z);
        }

        private void ResetProbeOffset()
        {
            if (!ToolOffset.HasValue)
                return;
            ModifyProbeOffset(p => new ProbeOffset(p.Key, ToolOffset.Value.X, ToolOffset.Value.Y, ToolOffset.Value.Z));
        }
        private async Task SetProbeOffsetAsync()
        {
            if (!ProbeOffsetKey.HasValue)
                return;

            var offset_x = new NumericOption("offset_x", "OFFSET X", ProbeOffset.ConvertOr(o => o.X, () => 0), 0.05);
            var offset_y = new NumericOption("offset_y", "OFFSET Y", ProbeOffset.ConvertOr(o => o.Y, () => 0), 0.05);
            var offset_z = new NumericOption("offset_z", "OFFSET Z", ProbeOffset.ConvertOr(o => o.Z, () => 0), 0.01);
            var result = await Flux.ShowSelectionAsync(
                "IMPOSTA OFFSET",
                new[] { offset_x, offset_y, offset_z });

            if (result != ContentDialogResult.Primary)
                return;

            ModifyProbeOffset(p => new ProbeOffset(p.Key, offset_x.Value, offset_y.Value, offset_z.Value));
        }
        public async Task ProbeOffsetAsync()
        {
            if (!ProbeOffsetKey.HasValue)
                return;

            var variable_store = Flux.ConnectionProvider.VariableStoreBase;
            var feeder_index = ArrayIndex.FromZeroBase(Feeder.Position, variable_store);

            var offset_z = await Flux.ConnectionProvider.ProbeOffsetAsync(feeder_index);
            if (!offset_z.HasValue)
                return;

            ModifyProbeOffset(p => new ProbeOffset(p.Key, p.X, p.Y, offset_z.Value));
        }
        public void ModifyUserOffset(Func<UserOffset, UserOffset> edit_func)
        {
            if (!UserOffsetKey.HasValue)
                return;

            var old_offset = UserOffset.ValueOr(() => new UserOffset(UserOffsetKey.Value, 0, 0, 0));
            var new_offset = edit_func(old_offset);

            var user_settings = Flux.SettingsProvider.UserSettings;
            user_settings.Local.UserOffsets.AddOrUpdate(new_offset);
        }
        public void ModifyProbeOffset(Func<ProbeOffset, ProbeOffset> edit_func)
        {
            if (!ProbeOffsetKey.HasValue)
                return;

            var old_offset = ProbeOffset.ValueOr(() =>
            {
                var feeder = Flux.Feeders.Feeders.Lookup(Position);
                var tool = feeder.Convert(f => f.ToolNozzle.Document.tool);
                if (!tool.HasValue)
                    return new ProbeOffset(ProbeOffsetKey.Value, 0, 0, 0);

                var x_offset = tool.Value[t => t.ToolXOffset, 0.0];
                var y_offset = tool.Value[t => t.ToolYOffset, 0.0];
                var z_offset = tool.Value[t => t.ToolZOffset, 0.0];

                return new ProbeOffset(ProbeOffsetKey.Value, x_offset, y_offset, z_offset);
            });

            var new_offset = edit_func(old_offset);
            var user_settings = Flux.SettingsProvider.UserSettings;
            user_settings.Local.ProbeOffsets.AddOrUpdate(new_offset);
        }
        private Optional<FluxOffset> GetOffset(Optional<double> global_z_offset, Optional<ProbeOffset> probe_offset, Optional<UserOffset> user_offset)
        {
            if (!global_z_offset.HasValue)
                return Optional<FluxOffset>.None;
            if (!probe_offset.HasValue)
                return Optional<FluxOffset>.None;
            return new FluxOffset(Feeder.Position, global_z_offset.Value, probe_offset.Value, user_offset);
        }
    }
}
