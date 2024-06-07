using DynamicData;
using DynamicData.Kernel;
using Microsoft.Extensions.Logging;
using Modulo3DNet;
using ReactiveUI;
using RestSharp;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    [DataContract]
    public class ProgramHistory 
    {
        [DataMember]
        public int IdProgramHistory { get; set; }

        [DataMember] 
        public string JobKey { get; set; }

        [DataMember]
        public string TaskCode { get; set; }

        [DataMember]
        public string GCodeMetadata { get; set; }

        [DataMember]
        public string BeginDate { get; set; }

        [DataMember]
        public string EndDate { get; set; }
    }

    [DataContract]
    public class TaskHistory
    {
        [DataMember]
        public int IdTask { get; set; }

        [DataMember]
        public string Code { get; set; }

        [DataMember]
        public string StartDate { get; set; }

        [DataMember]
        public string EndDate { get; set; }
    }

    public class LoggingProvider : ReactiveObjectRC<LoggingProvider>
    {
        public FluxViewModel Flux { get; }
        public LoggingProvider(FluxViewModel flux)
        {
            Flux = flux;

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

            mcode_events.SubscribeRC(async mcode_event_storage =>
            {
                try
                {
                    if (!mcode_event_storage.HasValue)
                        return;

                    foreach (var job_event_storage in mcode_event_storage.Value)
                    {
                        var mcode_vm = Flux.MCodes.AvaiableMCodes.Lookup(job_event_storage.Key);
                        if (!mcode_vm.HasValue)
                            continue;

                        foreach (var job_events in job_event_storage.Value)
                        {
                            var core_settings = Flux.SettingsProvider.CoreSettings.Local;
                            if (!core_settings.LoggerAddress.HasValue || string.IsNullOrEmpty(core_settings.LoggerAddress.Value))
                                continue;

                            var tasks_get_request = new RestRequest($"{core_settings.LoggerAddress}/api/tasks/opentasks");
                            var tasks_get_response = await Flux.NetProvider.Client.GetAsync(tasks_get_request);
                            var open_task = JsonUtils.Deserialize<TaskHistory[]>(tasks_get_response.Content);

                            var program_history = open_task
                                .Convert(open_tasks => open_tasks
                                    .FirstOrOptional(open_task => open_task.IdTask > -1))
                                .Convert(open_task => new ProgramHistory()
                                {
                                    TaskCode = open_task.Code,
                                    JobKey = job_events.Key.ToString(),
                                    GCodeMetadata = mcode_vm.Value.Analyzer.MCode.Serialize(),
                                });

                            foreach (var job_event in job_events.Value)
                            {
                                flux.Logger.LogInformation(new EventId(0, $"job_event"), $"{job_event}");

                                try
                                {
                                    if (program_history.HasValue)
                                    {
                                        switch (job_event.Event.Type)
                                        {
                                            case FluxEventType.Begin:
                                                program_history.Value.BeginDate = job_event.Event.DateTime.ToString();
                                                var programs_post_req = new RestRequest($"{core_settings.LoggerAddress}/api/programs");
                                                programs_post_req.AddJsonBody(program_history.Value);
                                                await Flux.NetProvider.Client.PostAsync(programs_post_req);
                                                break;
                                            case FluxEventType.End:
                                            case FluxEventType.Cancel:
                                                program_history.Value.EndDate = job_event.Event.DateTime.ToString();
                                                var programs_put_req = new RestRequest($"{core_settings.LoggerAddress}/api/programs");
                                                programs_put_req.AddJsonBody(program_history.Value);
                                                await Flux.NetProvider.Client.PutAsync(programs_put_req);
                                                break;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    flux.Logger.LogError(new EventId(0, ""), ex.Message);
                                    return;
                                }
                            }

                            using var delete_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            await Flux.ConnectionProvider.DeleteAsync(c => c.JobEventPath, $"{job_event_storage.Key};{job_events.Key}", delete_cts.Token);
                        }
                    }
                }
                catch(Exception ex) 
                { 
                    Console.WriteLine(ex.Message);
                }
            }, this);

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
            }, this);
        }
    }

    /*public static class LoggingExtentions
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
    }*/
}
