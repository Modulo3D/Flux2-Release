using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Kernel;
using Microsoft.Extensions.Logging;
using Modulo3DStandard;
using ReactiveUI;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class LoggingProvider : ReactiveObject
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
                .Select(q => (OldValue:q.OldValue.Value, NewValue:q.NewValue.Value));

            var queue_ended = queue_pos.PairWithPreviousValue()
                .Where(q => q.OldValue.HasValue && q.OldValue.Value > -1)
                .Where(q => q.NewValue.HasValue && q.NewValue.Value == -1)
                .Select(q => q.OldValue.Value);
            
            var job_started = Observable.Merge(queue_started, queue_incremented.Select(i => i.NewValue))
                .SelectMany(q => progress.FirstAsync(b => b.Percentage > 0).Select(_ => q));

            var job_finished = Observable.Merge(queue_ended, queue_incremented.Select(i => i.OldValue));

            var mcode_events = Flux.ConnectionProvider
                .ObserveVariable(c => c.MCODE_EVENT);

            mcode_events.Subscribe(async events =>
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
                        try
                        {
                            var program_history = new JobHistory()
                            {
                                JobKey = job.Key.ToString(),
                                MCodeKey = mcode.Key.ToString(),
                                Name = mcode_vm.Value.Analyzer.MCode.Name,
                                GCodeMetadata = mcode_vm.Value.Analyzer.MCode.Serialize(),
                            };


                            foreach (var @event in job.Value)
                            {
                                flux.Logger.LogInformation(new EventId(0, $"job_started"), $"{@event}");
                                
                                // TODO
                                var core_settings = Flux.SettingsProvider.CoreSettings.Local;
                                if (!core_settings.LoggerAddress.HasValue)
                                    continue;
                       
                                switch (@event.Event.Event)
                                {
                                    case "start":
                                        program_history.BeginDate = @event.Event.DateTime.ToString();
                                        var request = new RestRequest($"{core_settings.LoggerAddress}/api/programs");
                                        request.AddJsonBody(program_history);
                                        await Flux.NetProvider.Client.PostAsync(request);
                                        break;
                                    case "stop":
                                        program_history.EndDate = @event.Event.DateTime.ToString();
                                        request = new RestRequest($"{core_settings.LoggerAddress}/api/programs");
                                        request.AddJsonBody(program_history);
                                        await Flux.NetProvider.Client.PutAsync(request);
                                        break;
                                }
                            }

                            using var delete_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            await Flux.ConnectionProvider.DeleteAsync(c => c.JobEventPath, $"{mcode.Key};{job.Key}", false, delete_cts.Token);
                        }
                        catch (Exception ex)
                        { 
                        }
                    }
                }
            });


            /*job_started.Subscribe(async queue_position =>
            {
                try
                {
                    var queue = await Flux.ConnectionProvider.ReadVariableAsync(c => c.QUEUE);
                    if (!queue.HasValue)
                        return;

                    var job = queue.Value.Lookup(queue_position);
                    if (!job.HasValue)
                        return;

                   
                }
                catch (Exception ex)
                {
                    flux.Logger.LogInformation(new EventId(0, "job_started"), ex.Message);
                }
            });

            job_finished.Subscribe(async queue_position => 
            {
                try
                {
                    var queue = await Flux.ConnectionProvider.ReadVariableAsync(c => c.QUEUE);
                    if (!queue.HasValue)
                        return;

                    var job = queue.Value.Lookup(queue_position);
                    if (!job.HasValue)
                        return;

                    var program_history = new JobHistory(job.Value)
                    {
                        EndDate = DateTime.Now.ToString(),
                    };

                    var core_settings = Flux.SettingsProvider.CoreSettings.Local;
                    if (!core_settings.LoggerAddress.HasValue)
                        return;

                    flux.Logger.LogInformation(new EventId(0, "job_finished"), $"{job.Value.JobKey}");

                    // TODO
                    if (!core_settings.LoggerAddress.HasValue)
                        return;
                    if (string.IsNullOrEmpty(core_settings.LoggerAddress.Value))
                        return;

                    var request = new RestRequest($"{core_settings.LoggerAddress}/api/programs");
                    request.AddJsonBody(program_history);
                    await Flux.NetProvider.Client.PutAsync(request);
                }
                catch (Exception ex)
                {
                    flux.Logger.LogInformation(new EventId(0, "job_finished"), ex.Message);
                }
            });*/
        }
    }

    public static class LoggingExtentions
    {
        public static void LogVariable<TRData, TWData, TLData>(
            this IFLUX_ConnectionProvider connection_provider, 
            ILogger logger, 
            Func<IFLUX_VariableStore, IFLUX_Variable<TRData, TWData>> get_variable,
            Func<TRData, TLData> get_log)
        {
            var variable = connection_provider.GetVariable(get_variable);
            var id = new EventId(0, variable.Name.ToLower().Replace(" ", "_"));
            variable.ValueChanged
                .Where(v => v.HasValue)
                .Select(v => get_log(v.Value))
                .DistinctUntilChanged()
                .Subscribe(v => logger.LogInformation(id, $"{v}"));
        }
        public static void LogVariable<TRData, TWData, TLData>(
            this IFLUX_ConnectionProvider connection_provider,
            ILogger logger,
            Func<IFLUX_VariableStore, Optional<IFLUX_Variable<TRData, TWData>>> get_variable,
            Func<TRData, TLData> get_log)
        {
            var variable = connection_provider.GetVariable(get_variable);
            if (!variable.HasValue)
                return;

            var id = new EventId(0, variable.Value.Name.ToLower().Replace(" ", "_"));
            variable.Value.ValueChanged
                .Where(v => v.HasValue)
                .Select(v => get_log(v.Value))
                .DistinctUntilChanged()
                .Subscribe(v => logger.LogInformation(id, $"{v}"));
        }
        public static void LogVariable<TRData, TWData, TLData>(
            this IFLUX_ConnectionProvider connection_provider,
            ILogger logger,
            Func<IFLUX_VariableStore, IFLUX_Array<TRData, TWData>> get_variable,
            VariableUnit unit,
            Func<TRData, TLData> get_log)
        {
            var variable = connection_provider.GetVariable(get_variable, unit);
            if (!variable.HasValue)
                return;

            var id = new EventId(0, variable.Value.Name.ToLower().Replace(" ", "_"));
            variable.Value.ValueChanged
                .Where(v => v.HasValue)
                .Select(v => get_log(v.Value))
                .DistinctUntilChanged()
                .Subscribe(v => logger.LogInformation(id, $"{v}"));
        }
        public static void LogVariable<TRData, TWData, TLData>(
            this IFLUX_ConnectionProvider connection_provider,
            ILogger logger,
            Func<IFLUX_VariableStore, Optional<IFLUX_Array<TRData, TWData>>> get_variable,
            VariableUnit unit,
            Func<TRData, TLData> get_log)
        {
            var variable = connection_provider.GetVariable(get_variable, unit);
            if (!variable.HasValue)
                return;

            var id = new EventId(0, variable.Value.Name.ToLower().Replace(" ", "_"));
            variable.Value.ValueChanged
                .Where(v => v.HasValue)
                .Select(v => get_log(v.Value))
                .DistinctUntilChanged()
                .Subscribe(v => logger.LogInformation(id, $"{v}"));
        }

        public static IObservable<T> LogObservable<T>(this IObservable<T> observable,
           EventId event_id,
           ILogger logger)
        {
            observable
               .DistinctUntilChanged()
               .Subscribe(v => logger.LogInformation(event_id, $"{v}"));
            return observable;
        }
    }
}
