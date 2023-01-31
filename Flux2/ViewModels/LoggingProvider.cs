using DynamicData;
using DynamicData.Kernel;
using Microsoft.Extensions.Logging;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class LoggingProvider : ReactiveObjectRC
    {
        public FluxViewModel Flux { get; }
        public CompositeDisposable Disposables { get; }
        public LoggingProvider(FluxViewModel flux)
        {
            Flux = flux;
            Disposables = new CompositeDisposable();

            // log program history
            var queue_pos = Flux.ConnectionProvider.ObserveVariable(c => c.QUEUE_POS);
            var progress = Flux.StatusProvider.WhenAnyValue(c => c.PrintProgress);

            var queue_started = queue_pos.PairWithPreviousValue()
                .Where(q => q.OldValue.HasValue && q.OldValue.Value == -1)
                .Where(q => q.NewValue.HasValue && q.NewValue.Value > q.OldValue.Value)
                .Select(q => q.NewValue.Value);

            var queue_incremented = queue_pos.PairWithPreviousValue()
                .Where(q => q.OldValue.HasValue && q.OldValue.Value > -1)
                .Where(q => q.NewValue.HasValue && q.NewValue.Value > q.OldValue.Value)
                .Select(q => (OldValue: q.OldValue.Value, NewValue: q.NewValue.Value));

            var queue_ended = queue_pos.PairWithPreviousValue()
                .Where(q => q.OldValue.HasValue && q.OldValue.Value > -1)
                .Where(q => q.NewValue.HasValue && q.NewValue.Value == -1)
                .Select(q => q.OldValue.Value);

            var job_started = Observable.Merge(queue_started, queue_incremented.Select(i => i.NewValue))
                .SelectMany(q => progress.FirstAsync(b => b.Percentage > 0).Select(_ => q));

            var job_finished = Observable.Merge(queue_ended, queue_incremented.Select(i => i.OldValue));

            var mcode_events = Flux.ConnectionProvider
                .ObserveVariable(c => c.MCODE_EVENT)
                .ConvertAsync(get_events)
                .StartWithDefault();

            Task<Optional<MCodeEventStorage>> get_events(MCodeEventStoragePreview events)
            {
                using var storage_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                return events.GetMCodeEventStorageAsync(Flux.ConnectionProvider, storage_cts.Token);
            };

            mcode_events.SubscribeRC(async events =>
            {
                if (!events.HasValue)
                    return;

                foreach (var mcode in events.Value)
                {
                    var mcode_vm = Flux.MCodes.AvaiableMCodes.Lookup(mcode.Key);
                    if (!mcode_vm.HasValue)
                        continue;

                    foreach (var job in mcode.Value)
                    {
                        foreach (var @event in job.Value)
                            flux.Logger.LogInformation(new EventId(0, $"job_event"), $"{@event}");

                        // TODO
                        //var program_history = new JobHistory()
                        //{
                        //    JobKey = job.Key.ToString(),
                        //    MCodeKey = mcode.Key.ToString(),
                        //    Name = mcode_vm.Value.Analyzer.MCode.Name,
                        //    GCodeMetadata = mcode_vm.Value.Analyzer.MCode.Serialize(),
                        //};

                        //foreach (var @event in job.Value)
                        //{
                        //    flux.Logger.LogInformation(new EventId(0, $"job_event"), $"{@event}");

                        //    
                        //    var core_settings = Flux.SettingsProvider.CoreSettings.Local;
                        //    if (!core_settings.LoggerAddress.HasValue)
                        //        continue;

                        //    try
                        //    {
                        //        switch (@event.Event.Event)
                        //        {
                        //            case :
                        //                program_history.BeginDate = @event.Event.DateTime.ToString();
                        //                var request = new RestRequest($"{core_settings.LoggerAddress}/api/programs");
                        //                request.AddJsonBody(program_history);
                        //                await Flux.NetProvider.Client.PostAsync(request);
                        //                break;
                        //            case "stop":
                        //                program_history.EndDate = @event.Event.DateTime.ToString();
                        //                request = new RestRequest($"{core_settings.LoggerAddress}/api/programs");
                        //                request.AddJsonBody(program_history);
                        //                await Flux.NetProvider.Client.PutAsync(request);
                        //                break;
                        //        }
                        //    }
                        //    catch (Exception ex)
                        //    { 
                        //    }
                        //}

                        using var delete_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await Flux.ConnectionProvider.DeleteAsync(c => c.JobEventPath, $"{mcode.Key};{job.Key}", delete_cts.Token);
                    }
                }
            }, Disposables);

            var extrusions = Flux.StatusProvider.FeederEvaluators.Connect()
                .AutoTransform(f => f.ExtrusionQueue)
                .QueryWhenChanged();

            extrusions.SubscribeRC(extrusions =>
            {
                foreach (var extrusion in extrusions.KeyValues)
                {
                    if (!extrusion.Value.HasValue)
                        continue;
                    var total_weight = extrusion.Value.Value.Aggregate(0.0, (w, kvp) => w + kvp.Value.WeightG);
                    flux.Logger.LogInformation(new EventId(0, $"job_event"), $"{extrusion.Key}:{total_weight:0.##}".Replace(",", "."));
                }
            }, Disposables);
        }
    }

    public static class LoggingExtentions
    {
        public static void LogVariable<TRData, TWData, TLData>(
            this IFLUX_ConnectionProvider connection_provider,
            ILogger logger,
            Func<IFLUX_VariableStore, IFLUX_Variable<TRData, TWData>> get_variable,
            Func<TRData, TLData> get_log,
            CompositeDisposable d)
        {
            var variable = connection_provider.GetVariable(get_variable);
            var id = new EventId(0, variable.Name.ToLower().Replace(" ", "_"));
            variable.ValueChanged
                .Where(v => v.HasValue)
                .Select(v => get_log(v.Value))
                .DistinctUntilChanged()
                .SubscribeRC(v => logger.LogInformation(id, $"{v}"), d);
        }
        public static void LogVariable<TRData, TWData, TLData>(
            this IFLUX_ConnectionProvider connection_provider,
            ILogger logger,
            Func<IFLUX_VariableStore, Optional<IFLUX_Variable<TRData, TWData>>> get_variable,
            Func<TRData, TLData> get_log,
            CompositeDisposable d)
        {
            var variable = connection_provider.GetVariable(get_variable);
            if (!variable.HasValue)
                return;

            var id = new EventId(0, variable.Value.Name.ToLower().Replace(" ", "_"));
            variable.Value.ValueChanged
                .Where(v => v.HasValue)
                .Select(v => get_log(v.Value))
                .DistinctUntilChanged()
                .SubscribeRC(v => logger.LogInformation(id, $"{v}"), d);
        }
        public static void LogVariable<TRData, TWData, TLData>(
            this IFLUX_ConnectionProvider connection_provider,
            ILogger logger,
            Func<IFLUX_VariableStore, IFLUX_Array<TRData, TWData>> get_variable,
            VariableAlias alias,
            Func<TRData, TLData> get_log,
            CompositeDisposable d)
        {
            var variable = connection_provider.GetVariable(get_variable, alias);
            if (!variable.HasValue)
                return;

            var id = new EventId(0, variable.Value.Name.ToLower().Replace(" ", "_"));
            variable.Value.ValueChanged
                .Where(v => v.HasValue)
                .Select(v => get_log(v.Value))
                .DistinctUntilChanged()
                .SubscribeRC(v => logger.LogInformation(id, $"{v}"), d);
        }
        public static void LogVariable<TRData, TWData, TLData>(
            this IFLUX_ConnectionProvider connection_provider,
            ILogger logger,
            Func<IFLUX_VariableStore, Optional<IFLUX_Array<TRData, TWData>>> get_variable,
            VariableAlias alias,
            Func<TRData, TLData> get_log,
            CompositeDisposable d)
        {
            var variable = connection_provider.GetVariable(get_variable, alias);
            if (!variable.HasValue)
                return;

            var id = new EventId(0, variable.Value.Name.ToLower().Replace(" ", "_"));
            variable.Value.ValueChanged
                .Where(v => v.HasValue)
                .Select(v => get_log(v.Value))
                .DistinctUntilChanged()
                .SubscribeRC(v => logger.LogInformation(id, $"{v}"), d);
        }

        public static IObservable<T> LogObservable<T>(this IObservable<T> observable,
            EventId event_id,
            ILogger logger,
            CompositeDisposable d)
        {
            observable
               .DistinctUntilChanged()
               .SubscribeRC(v => logger.LogInformation(event_id, $"{v}"), d);
            return observable;
        }
    }
}
