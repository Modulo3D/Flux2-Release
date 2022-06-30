using DynamicData.Kernel;
using Microsoft.Extensions.Logging;
using Modulo3DStandard;
using ReactiveUI;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class LoggingProvider : ReactiveObject
    {
        public FluxViewModel Flux { get; }
        public LoggingProvider(FluxViewModel flux)
        {
            Flux = flux;

            // log variables
            Flux.ConnectionProvider.LogVariable(Flux.Logger, c => c.TEMP_CHAMBER, "spools", t => t.Target);

            // log program history
            var queue_pos = Flux.ConnectionProvider.ObserveVariable(c => c.QUEUE_POS);
            var block_num = Flux.ConnectionProvider.ObserveVariable(c => c.BLOCK_NUM);

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
                .SelectMany(q => block_num.FirstAsync(b => b.HasValue && b.Value > 0).Select(_ => q));

            var job_finished = Observable.Merge(queue_ended, queue_incremented.Select(i => i.OldValue));

            job_started.Subscribe(async queue_position =>
            {
                try
                {
                    var queue = await Flux.ConnectionProvider.ReadVariableAsync(c => c.QUEUE);
                    if (!queue.HasValue)
                        return;

                    var job = queue.Value.Lookup(queue_position);
                    if (!job.HasValue)
                        return;

                    var mcode_vm = Flux.MCodes.AvaiableMCodes.Lookup(job.Value.MCodeGuid);
                    if (!mcode_vm.HasValue)
                        return;

                    var mcode = mcode_vm.Value.Analyzer.Convert(a => a.MCode);
                    if (!mcode.HasValue)
                        return;

                    var program_history = new ProgramsHistory(job.Value.JobGuid)
                    {
                        Name = mcode.Value.Name,
                        BeginDate = DateTime.Now.ToString(),
                        GCodeMetadata = mcode.Value.Serialize(),
                    };

                    var core_settings = Flux.SettingsProvider.CoreSettings.Local;
                    if (!core_settings.LoggerAddress.HasValue)
                        return;

                    flux.Logger.LogInformation(new EventId(0, "job_started"), $"{job.Value.JobGuid}");

                    // todo
                    var request = new RestRequest($"{core_settings.LoggerAddress}/api/programs");
                    request.AddJsonBody(program_history);
                    await Flux.NetProvider.Client.PostAsync(request);
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

                    var program_history = new ProgramsHistory(job.Value.JobGuid)
                    {
                        EndDate = DateTime.Now.ToString(),
                    };

                    var core_settings = Flux.SettingsProvider.CoreSettings.Local;
                    if (!core_settings.LoggerAddress.HasValue)
                        return;

                    flux.Logger.LogInformation(new EventId(0, "job_finished"), $"{job.Value.JobGuid}");

                    // todo
                    var request = new RestRequest($"{core_settings.LoggerAddress}/api/programs");
                    request.AddJsonBody(program_history);
                    await Flux.NetProvider.Client.PutAsync(request);
                }
                catch (Exception ex)
                {
                    flux.Logger.LogInformation(new EventId(0, "job_finished"), ex.Message);
                }
            });
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
            VariableAlias alias,
            Func<TRData, TLData> get_log)
        {
            var variable = connection_provider.GetVariable(get_variable, alias);
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
            VariableAlias alias,
            Func<TRData, TLData> get_log)
        {
            var variable = connection_provider.GetVariable(get_variable, alias);
            if (!variable.HasValue)
                return;

            var id = new EventId(0, variable.Value.Name.ToLower().Replace(" ", "_"));
            variable.Value.ValueChanged
                .Where(v => v.HasValue)
                .Select(v => get_log(v.Value))
                .DistinctUntilChanged()
                .Subscribe(v => logger.LogInformation(id, $"{v}"));
        }
    }
}
