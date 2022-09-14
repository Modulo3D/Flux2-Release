﻿using DynamicData;
using DynamicData.Binding;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class FeedersViewModel : FluxRoutableNavBarViewModel<FeedersViewModel>, IFluxFeedersViewModel
    {
        [RemoteContent(true)]
        public IObservableCache<IFluxFeederViewModel, ushort> Feeders { get; private set; }

        public ObservableAsPropertyHelper<Optional<FLUX_Humidity>> _Humidity;
        [RemoteOutput(true, typeof(HumidityConverter))]
        public Optional<FLUX_Humidity> Humidity => _Humidity.Value;

        public ObservableAsPropertyHelper<short> _SelectedExtruder;
        public short SelectedExtruder => _SelectedExtruder.Value;

        public ObservableAsPropertyHelper<Optional<IFluxFeederViewModel>> _SelectedFeeder;
        public Optional<IFluxFeederViewModel> SelectedFeeder => _SelectedFeeder.Value;

        public ObservableAsPropertyHelper<bool> _HasInvalidStates;
        public bool HasInvalidStates => _HasInvalidStates.Value;

        public IObservableCache<Optional<Material>, ushort> Materials { get; }
        public IObservableCache<Optional<Nozzle>, ushort> Nozzles { get; }
        public IObservableCache<Optional<Tool>, ushort> Tools { get; }

        public FeedersViewModel(FluxViewModel flux) : base(flux)
        {
            Feeders = Flux.SettingsProvider
                .WhenAnyValue(v => v.ExtrudersCount)
                .Select(CreateFeeders)
                .ToObservableChangeSet(f => f.Position)
                .DisposeMany()
                .AsObservableCache()
                .DisposeWith(Disposables);

            _HasInvalidStates = Feeders.Connect()
                .TrueForAny(f => f.HasInvalidStateChanged, i => i)
                .ToProperty(this, f => f.HasInvalidStates)
                .DisposeWith(Disposables);

            // TODO
            Materials = Feeders.Connect()
                .AutoRefresh(f => f.SelectedMaterial)
                .Transform(f => f.SelectedMaterial, true)
                .AutoRefresh(m => m.Document)
                .Transform(f => f.Convert(f => f.Document), true)
                .AsObservableCache()
                .DisposeWith(Disposables);

            Tools = Feeders.Connect()
                .AutoRefresh(f => f.ToolNozzle.Document)
                .Transform(f => f.ToolNozzle.Document, true)
                .Transform(tn => tn.tool)
                .AsObservableCache()
                .DisposeWith(Disposables);

            Nozzles = Feeders.Connect()
              .AutoRefresh(f => f.ToolNozzle.Document)
              .Transform(f => f.ToolNozzle.Document, true)
              .Transform(tn => tn.nozzle)
              .AsObservableCache()
              .DisposeWith(Disposables);

            var selected_extruder = Flux.ConnectionProvider
                .ObserveVariable(m => m.TOOL_ON_TRAILER)
                .Convert(c => c.QueryWhenChanged(q => (short)q.Items.IndexOf(true)))
                .ToOptionalObservable();

            _SelectedExtruder = selected_extruder
                .ObservableOr(() => (short)-1)
                .ToProperty(this, v => v.SelectedExtruder)
                .DisposeWith(Disposables);

            _SelectedFeeder = selected_extruder
                .ConvertToObservable(FindSelectedFeeder)
                .ObservableOrDefault()
                .ToProperty(this, v => v.SelectedFeeder)
                .DisposeWith(Disposables);

            var humidity_units = Flux.ConnectionProvider.GetArrayUnits(c => c.FILAMENT_HUMIDITY);
            _Humidity = Flux.SettingsProvider.WhenAnyValue(s => s.ExtrudersCount)
                .SelectMany(e =>
                {
                    if (!e.HasValue)
                        return Observable.Return(Optional<FLUX_Humidity>.None);
                    var spool_count = e.Value.machine_extruders * e.Value.mixing_extruders;
                    if (humidity_units.Count() != 1 && spool_count == 1)
                        return Observable.Return(Optional<FLUX_Humidity>.None);
                    var humidity_unit = Flux.ConnectionProvider.GetArrayUnit(c => c.FILAMENT_HUMIDITY, 0);
                    if (!humidity_unit.HasValue)
                        return Observable.Return(Optional<FLUX_Humidity>.None);
                    var humidity = Flux.ConnectionProvider.ObserveVariable(c => c.FILAMENT_HUMIDITY, humidity_unit.Value.Alias);
                    if (!humidity.HasObservable)
                        return Observable.Return(Optional<FLUX_Humidity>.None);
                    return humidity.Observable;
                })
                .ToProperty(this, v => v.Humidity);
        }

        private IObservable<Optional<IFluxFeederViewModel>> FindSelectedFeeder(short selected_extruder)
        {
            if (selected_extruder < 0)
                return Observable.Return(Optional<IFluxFeederViewModel>.None);

            return Feeders.Connect().WatchValue((ushort)selected_extruder)
                .Select(f => Optional<IFluxFeederViewModel>.Create(f));
        }

        private IEnumerable<IFluxFeederViewModel> CreateFeeders(Optional<(ushort machine_extruders, ushort mixing_extruders)> extruders)
        {
            if (!extruders.HasValue)
                yield break;
            for (ushort position = 0; position < extruders.Value.machine_extruders; position++)
                yield return new FeederViewModel(this, position);
        }
    }
}
