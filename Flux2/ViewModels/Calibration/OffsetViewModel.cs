﻿using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
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

        private ObservableAsPropertyHelper<Optional<ToolId>> _ToolId;
        public Optional<ToolId> ToolId => _ToolId.Value;

        private ObservableAsPropertyHelper<Optional<UserOffsetKey>> _UserOffsetKey;
        [RemoteOutput(true, typeof(HasValueConverter<UserOffsetKey>))]
        public Optional<UserOffsetKey> UserOffsetKey => _UserOffsetKey.Value;

        private ObservableAsPropertyHelper<Optional<ProbeOffsetKey>> _ProbeOffsetKey;
        public Optional<ProbeOffsetKey> ProbeOffsetKey => _ProbeOffsetKey.Value;

        private ObservableAsPropertyHelper<Optional<ToolOffset>> _ToolOffset;
        public Optional<ToolOffset> ToolOffset => _ToolOffset.Value;

        private ObservableAsPropertyHelper<Optional<UserOffset>> _UserOffset;
        public Optional<UserOffset> UserOffset => _UserOffset.Value;

        private ObservableAsPropertyHelper<Optional<ProbeOffset>> _ProbeOffset;
        public Optional<ProbeOffset> ProbeOffset => _ProbeOffset.Value;

        private ObservableAsPropertyHelper<bool> _IsOffsetRoot;
        [RemoteOutput(true)]
        public bool IsOffsetRoot => _IsOffsetRoot.Value;

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> SetProbeOffsetCommand { get; }
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ResetProbeOffsetCommand { get; }

        private double _XUserOffset;
        [RemoteInput(0.05, converter: typeof(MillimeterConverter))]
        public double XUserOffset
        {
            get => _XUserOffset;
            set => this.RaiseAndSetIfChanged(ref _XUserOffset, value);
        }

        private double _YUserOffset;
        [RemoteInput(0.05, converter: typeof(MillimeterConverter))]
        public double YUserOffset
        {
            get => _YUserOffset;
            set => this.RaiseAndSetIfChanged(ref _YUserOffset, value);
        }

        private double _ZUserOffset;
        [RemoteInput(0.01, converter: typeof(MillimeterConverter))]
        public double ZUserOffset
        {
            get => _ZUserOffset;
            set => this.RaiseAndSetIfChanged(ref _ZUserOffset, value);
        }

        private ObservableAsPropertyHelper<double> _XProbeOffset;
        [RemoteOutput(true, converter: typeof(MillimeterConverter))]
        public double XProbeOffset => _XProbeOffset.Value;

        private ObservableAsPropertyHelper<double> _YProbeOffset;
        [RemoteOutput(true, converter: typeof(MillimeterConverter))]
        public double YProbeOffset => _YProbeOffset.Value;

        private ObservableAsPropertyHelper<double> _ZProbeOffset;
        [RemoteOutput(true, converter: typeof(MillimeterConverter))]
        public double ZProbeOffset => _ZProbeOffset.Value;

        private ObservableAsPropertyHelper<FluxProbeState> _ProbeState;
        [RemoteOutput(true)]
        public FluxProbeState ProbeState => _ProbeState.Value;

        private ObservableAsPropertyHelper<string> _ProbeStateBrush;
        [RemoteOutput(true)]
        public string ProbeStateBrush => _ProbeStateBrush.Value;

        [RemoteOutput(false)]
        public ushort Position => Feeder.Position;

        public OffsetViewModel(CalibrationViewModel calibration, IFluxFeederViewModel feeder) : base($"offset??{feeder.Position}")
        {
            Feeder = feeder;
            Flux = calibration.Flux;
            Calibration = calibration;

            var user_settings = Flux.SettingsProvider.UserSettings;

            _ToolId = Feeder.ToolNozzle
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
                .ToProperty(this, v => v.ToolId);

            _ToolOffset = Observable.CombineLatest(
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                Feeder.ToolNozzle.WhenAnyValue(v => v.Nfc),
                (db, nfc) =>
                {
                    if (!db.HasValue)
                        return Optional<ToolOffset>.None;
                    if (!nfc.Tag.HasValue)
                        return Optional<ToolOffset>.None;
                    var tool = nfc.Tag.Value.GetDocument<Tool>(db.Value, tn => tn.ToolGuid);
                    if (!tool.HasValue)
                        return Optional<ToolOffset>.None;
                    var x = tool.Value.ToolXOffset.ValueOr(() => 0);
                    var y = tool.Value.ToolYOffset.ValueOr(() => 0);
                    var z = tool.Value.ToolZOffset.ValueOr(() => 0);
                    return new ToolOffset(x, y, z);
                })
                .ToProperty(this, v => v.ToolOffset);

            _UserOffsetKey = Observable.CombineLatest(
                Calibration.WhenAnyValue(v => v.GroupId),
                Feeder.ToolNozzle.WhenAnyValue(v => v.Nfc),
                (group_id, tool_reading) =>
                {
                    if (!group_id.HasValue)
                        return Optional<UserOffsetKey>.None;
                    if (!tool_reading.CardId.HasValue)
                        return Optional<UserOffsetKey>.None;
                    if (!tool_reading.Tag.HasValue)
                        return Optional<UserOffsetKey>.None;

                    var tool_nfc = tool_reading.Tag.Value;
                    var tool_card = tool_reading.CardId.Value;
                    var relative_id = new ToolId(Feeder.Position, tool_card, tool_nfc);

                    return new UserOffsetKey(group_id.Value, relative_id);
                })
                .ToProperty(this, v => v.UserOffsetKey)
                .DisposeWith(Disposables);

            _ProbeOffsetKey = Observable.CombineLatest(
                Flux.DatabaseProvider.WhenAnyValue(v => v.Database),
                Feeder.ToolNozzle.WhenAnyValue(v => v.Nfc),
                Feeder.Material.WhenAnyValue(v => v.Nfc),
                (db, tool_reading, material_reading) =>
                {
                    if (!tool_reading.CardId.HasValue)
                        return Optional<ProbeOffsetKey>.None;
                    if (!tool_reading.Tag.HasValue)
                        return Optional<ProbeOffsetKey>.None;
                    if (!material_reading.Tag.HasValue)
                        return Optional<ProbeOffsetKey>.None;

                    var tool_nfc = tool_reading.Tag.Value;
                    var tool_card = tool_reading.CardId.Value;
                    var relative_id = new ToolId(Feeder.Position, tool_card, tool_nfc);

                    var probe_offset_key = new ProbeOffsetKey(relative_id, material_reading.Tag.Value.MaterialGuid);
                    var probe_offset_lookup = user_settings.Local.ProbeOffsets.Lookup(probe_offset_key);
                    if (!probe_offset_lookup.HasValue)
                    {
                        if (db.HasValue) 
                        {
                            var nozzle = tool_nfc.GetDocument<Tool>(db.Value, tn => tn.ToolGuid);
                            if (nozzle.HasValue)
                            {
                                var x = nozzle.Value.ToolXOffset.ValueOr(() => 0);
                                var y = nozzle.Value.ToolYOffset.ValueOr(() => 0);
                                var z = nozzle.Value.ToolZOffset.ValueOr(() => 0);
                                var probe_offset = new ProbeOffset(probe_offset_key, x, y, z);
                                user_settings.Local.ProbeOffsets.AddOrUpdate(probe_offset);
                            }
                        }
                    }
                    return probe_offset_key;
                })
                .ToProperty(this, v => v.ProbeOffsetKey)
                .DisposeWith(Disposables);

            _UserOffset = user_settings.Local.UserOffsets.Connect()
                .WatchOptional(this.WhenAnyValue(v => v.UserOffsetKey))
                .ToProperty(this, v => v.UserOffset);

            _ProbeOffset = user_settings.Local.ProbeOffsets.Connect()
                .WatchOptional(this.WhenAnyValue(v => v.ProbeOffsetKey))
                .ToProperty(this, v => v.ProbeOffset)
                .DisposeWith(Disposables);

            _ProbeState = Observable.CombineLatest(
                this.WhenAnyValue(s => s.ToolOffset),
                this.WhenAnyValue(s => s.ProbeOffset),
                Feeder.ToolNozzle.WhenAnyValue(f => f.State),
                Feeder.Material.WhenAnyValue(f => f.State),
                (tool_offset, probe_offset, tool_state, material_state) => 
                {
                    if (!tool_state.IsLoaded() || !material_state.IsLoaded())
                        return FluxProbeState.NO_PROBE;

                    if (!tool_state.IsInMagazine() && !tool_state.IsOnTrailer())
                        return FluxProbeState.ERROR_PROBE;

                    if (!tool_offset.HasValue)
                        return FluxProbeState.ERROR_PROBE;

                    if (probe_offset.HasValue && 
                       (Math.Abs(probe_offset.Value.X - tool_offset.Value.X) > 5 ||
                        Math.Abs(probe_offset.Value.Y - tool_offset.Value.Y) > 5))
                        return FluxProbeState.ERROR_PROBE;

                    if (!probe_offset.HasValue ||
                        probe_offset.Value.Z >= tool_offset.Value.Z)
                        return FluxProbeState.INVALID_PROBE;

                    return FluxProbeState.VALID_PROBE;
                })
                .ToProperty(this, v => v.ProbeState)
                .DisposeWith(Disposables);

            _IsOffsetRoot = this.WhenAnyValue(v => v.UserOffsetKey)
                .Convert(o => o.RelativeTool.Position == o.GroupTool.Position)
                .ValueOr(() => false)
                .ToProperty(this, v => v.IsOffsetRoot)
                .DisposeWith(Disposables);

            var has_user_offset_key = Observable.CombineLatest(
                Flux.StatusProvider.IsIdle.ValueOrDefault(),
                this.WhenAnyValue(v => v.UserOffsetKey),
                (i, p) => i && p.HasValue);

            Observable.CombineLatest(
                this.WhenAnyValue(v => v.ProbeOffset),
                this.WhenAnyValue(v => v.UserOffset),
                (_, _) => Unit.Default)
                .Throttle(TimeSpan.FromSeconds(1))
                .Subscribe(_ => user_settings.PersistLocalSettings())
                .DisposeWith(Disposables);

            var has_probe_offset_key = Observable.CombineLatest(
                Flux.StatusProvider.IsIdle.ValueOrDefault(),
                this.WhenAnyValue(v => v.ProbeOffsetKey),
                (i, p) => i && p.HasValue);

            ResetProbeOffsetCommand = ReactiveCommand.Create(ResetProbeOffset, has_probe_offset_key)
                .DisposeWith(Disposables);

            SetProbeOffsetCommand = ReactiveCommand.CreateFromTask(SetProbeOffsetAsync, has_probe_offset_key)
                .DisposeWith(Disposables);

            var userOffset = this.WhenAnyValue(v => v.UserOffset);
            var probeOffset = this.WhenAnyValue(v => v.ProbeOffset);

            userOffset
                .ConvertOr(o => o.X, () => 0)
                .BindTo(this, v => v.XUserOffset)
                .DisposeWith(Disposables);
            this.WhenAnyValue(v => v.XUserOffset)
                .Subscribe(x => ModifyUserOffset(o => new UserOffset(o.Key, x, o.Y, o.Z)))
                .DisposeWith(Disposables);

            userOffset
                .ConvertOr(o => o.Y, () => 0)
                .BindTo(this, v => v.YUserOffset)
                .DisposeWith(Disposables);
            this.WhenAnyValue(v => v.YUserOffset)
                .Subscribe(y => ModifyUserOffset(o => new UserOffset(o.Key, o.X, y, o.Z)))
                .DisposeWith(Disposables);

            userOffset.ConvertOr(o => o.Z, () => 0)
              .BindTo(this, v => v.ZUserOffset)
              .DisposeWith(Disposables);
            this.WhenAnyValue(v => v.ZUserOffset)
                .Subscribe(z => ModifyUserOffset(o => new UserOffset(o.Key, o.X, o.Y, z)))
                .DisposeWith(Disposables);

            _XProbeOffset = probeOffset
                .ConvertOr(o => o.X, () => 0)
                .ToProperty(this, v => v.XProbeOffset)
                .DisposeWith(Disposables);

            _YProbeOffset = probeOffset
                .ConvertOr(o => o.Y, () => 0)
                .ToProperty(this, v => v.YProbeOffset)
                .DisposeWith(Disposables);

            _ZProbeOffset = probeOffset
                .ConvertOr(o => o.Z, () => 0)
                .ToProperty(this, v => v.ZProbeOffset)
                .DisposeWith(Disposables);

            _ProbeStateBrush = this.WhenAnyValue(v => v.ProbeState)
                .Select(state =>
                {
                    switch (state)
                    {
                        case FluxProbeState.INVALID_PROBE:
                            return FluxColors.Warning;
                        case FluxProbeState.NO_PROBE:
                            return FluxColors.Inactive;
                        case FluxProbeState.VALID_PROBE:
                            return FluxColors.Selected;
                        default:
                            return FluxColors.Error;
                    }
                })
                .ToProperty(this, v => v.ProbeStateBrush)
                .DisposeWith(Disposables);

            Observable.CombineLatest(
                Calibration.WhenAnyValue(v => v.GlobalZOffset),
                this.WhenAnyValue(v => v.ProbeOffset),
                this.WhenAnyValue(v => v.UserOffset),
                GetOffset)
                .Throttle(TimeSpan.FromSeconds(1))
                .Where(o => o.HasValue)
                .Subscribe(async o => await Flux.ConnectionProvider.SetToolOffsetsAsync(o.Value));
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

            using var dialog = new ContentDialog(Flux, "IMPOSTA OFFSET", confirm: () => { }, cancel: () => { });

            var offset_x = new NumericOption("offset_x", "OFFSET X", 0, 0.05);
            var offset_y = new NumericOption("offset_y", "OFFSET Y", 0, 0.05);
            var offset_z = new NumericOption("offset_z", "OFFSET Z", 0, 0.01);

            offset_x.Value = ProbeOffset.ConvertOr(o => o.X, () => 0);
            offset_y.Value = ProbeOffset.ConvertOr(o => o.Y, () => 0); 
            offset_z.Value = ProbeOffset.ConvertOr(o => o.Z, () => 0);
            
            dialog.AddContent(offset_x);
            dialog.AddContent(offset_y);
            dialog.AddContent(offset_z);

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;

            ModifyProbeOffset(p => new ProbeOffset(p.Key, offset_x.Value, offset_y.Value, offset_z.Value));
        }
        public async Task ProbeOffsetAsync()
        {
            if (!ProbeOffsetKey.HasValue)
                return;

            var settings = Flux.SettingsProvider.UserSettings.Local;

            var offset_z = await Flux.ConnectionProvider.ProbeOffsetAsync(Feeder.Position);
            if (!offset_z.HasValue)
                return;

            if (offset_z.Value == 0)
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

                var x_offset = tool.Value.ToolXOffset.ValueOr(() => 0);
                var y_offset = tool.Value.ToolYOffset.ValueOr(() => 0);
                var z_offset = tool.Value.ToolZOffset.ValueOr(() => 0);

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
