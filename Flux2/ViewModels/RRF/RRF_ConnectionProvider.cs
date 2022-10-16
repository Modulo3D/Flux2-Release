using DynamicData;
using DynamicData.Kernel;
using GreenSuperGreen.Queues;
using Modulo3DStandard;
using ReactiveUI;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public enum RRF_ConnectionPhase
    {
        START_PHASE = 0,
        DISCONNECTING_CLIENT = 1,
        CONNECTING_CLIENT = 2,
        READING_STATUS = 3,
        CREATING_VARIABLES = 4,
        INITIALIZING_VARIABLES = 5,
        READING_MEMORY = 6,
        END_PHASE = 7,
    }

    public class RRF_GCodeGenerator : FLUX_GCodeGenerator<RRF_Connection>
    {
        public RRF_GCodeGenerator(RRF_Connection connection) : base(connection)
        { 
        }

        public override GCodeString DeclareGlobalArray<T>(string array_name, int count, out GCodeArray<T> array)
        {
            array = new GCodeArray<T>()
            {
                Name = array_name,
                Variables = Enumerable.Range(0, count).Select(i =>
                {
                    var variable_name = $"{array_name}_{i}";
                    DeclareGlobalVariable<T>(variable_name, out var variable);
                    return variable;
                }).ToArray(),
            };
            return array.Foreach((v, i) => v.Declare);
        }
        public override GCodeString DeclareLocalArray<T>(string array_name, int count, object value, out GCodeArray<T> array)
        {
            array = new GCodeArray<T>()
            {
                Name = array_name,
                Variables = Enumerable.Range(0, count).Select(i =>
                {
                    var variable_name = $"{array_name}_{i}";
                    DeclareLocalVariable<T>(variable_name, value, out var variable);
                    return variable;
                }).ToArray(),
            };
            return array.Foreach((v, i) => v.Declare);
        }
        public override GCodeString DeclareGlobalVariable<T>(string variable_name, out GCodeVariable<T> variable)
        {
            variable = new GCodeVariable<T>()
            {
                Name = variable_name,
                Read = $"global.{variable_name}",
                Declare = new[]
                {
                    $"if !exists(global.{variable_name})",
                    $"    global {variable_name} = {(typeof(T) == typeof(string) ? $"\"{default(T)}\"" : default(T))}",
                },
                Write = v => $"set global.{variable_name} = {(typeof(T) == typeof(string) ? $"\"{v}\"" : $"{v}")}",
            };
            return variable.Declare;
        }
        public override GCodeString DeclareLocalVariable<T>(string variable_name, object value, out GCodeVariable<T> variable)
        {
            variable = new GCodeVariable<T>()
            {
                Name = variable_name,
                Read = $"var.{variable_name}",
                Declare = $"var {variable_name} = {(typeof(T) == typeof(string) ? $"\"{value}\"" : value)}",
                Write = v => $"set var.{variable_name} = {(typeof(T) == typeof(string) ? $"\"{v}\"" : $"{v}")}",
            };
            return variable.Declare;
        }

        public GCodeString DeclareGlobalArray<T>(int count, out GCodeArray<T> array, [CallerArgumentExpression("array")] string array_name = null)
        {
            array_name = array_name.Split(" ").LastOrDefault();
            return DeclareGlobalArray(array_name, count, out array);    
        }
        public GCodeString DeclareLocalArray<T>(int count, object value, out GCodeArray<T> array, [CallerArgumentExpression("array")] string array_name = null)
        {
            array_name = array_name.Split(" ").LastOrDefault();
            return DeclareLocalArray(array_name, count, value, out array);
        }
        public GCodeString DeclareGlobalVariable<T>(out GCodeVariable<T> variable, [CallerArgumentExpression("variable")] string variable_name = null)
        {
            variable_name = variable_name.Split(" ").LastOrDefault();
            return DeclareGlobalVariable(variable_name, out variable);   
        }
        public GCodeString DeclareLocalVariable<T>(object value, out GCodeVariable<T> variable, [CallerArgumentExpression("variable")] string variable_name = null)
        {
            variable_name = variable_name.Split(" ").LastOrDefault();
            return DeclareLocalVariable(variable_name, value, out variable);
        }

        public override GCodeString AppendFile(string folder, string name, string source)
        {
            var path = Connection.CombinePaths(folder, name);
            return new[]
            {
                "; appending file {name}",
                $"echo >>\"0:/{path}\" {source}",
            };
        }
        public override GCodeString CreateFile(string folder, string name, string source)
        {
            var path = Connection.CombinePaths(folder, name);
            return new[]
            {
                $"; creating file {name}",
                $"echo >\"0:/{path}\" {source}"
            };
        }
        public override GCodeString LogExtrusion(Job job, ExtrusionKey e, GCodeVariable<double> v)
        {
            if (job.QueuePosition < 0)
                return default;

            var folder = Connection.ExtrusionEventPath;
            return AppendFile(folder, $"{e}", $"\"{job.JobKey};\"^{v}");
        }
        public override GCodeString RenameFile(string old_path, string new_path)
        {
            return new[]
            {
                $"; renaming file {old_path}",
                $"M471 S{old_path} T{new_path}",
            };
        }
        public override GCodeString ExecuteParamacro(GCodeVariable<string> path)
        {
            return $"M98 P{{{path}}}";
        }
        public override GCodeString LogEvent<T>(Job job, T @event)
        {
            if (job.QueuePosition < 0)
                return default;

            var folder = Connection.JobEventPath;
            return AppendFile(folder, $"{job.MCodeKey};{job.JobKey}", $"\"{@event.ToEnumString()};\"^{{state.time}}");
        }
    }

    public class RRF_ConnectionProvider : FLUX_ConnectionProvider<RRF_ConnectionProvider, RRF_Connection, RRF_MemoryBuffer, RRF_VariableStoreBase, RRF_ConnectionPhase>
    {
        public FluxViewModel Flux { get; }

        public RRF_ConnectionProvider(FluxViewModel flux, Func<RRF_ConnectionProvider, RRF_VariableStoreBase> get_variable_store) : base(flux,
            RRF_ConnectionPhase.START_PHASE, RRF_ConnectionPhase.END_PHASE, p => (int)p,
            get_variable_store, c => new RRF_Connection(flux, c), c => new RRF_MemoryBuffer(c))
        {
            Flux = flux;
        }
        protected override async Task RollConnectionAsync()
        {
            try
            {
                switch (ConnectionPhase)
                {
                    case RRF_ConnectionPhase.START_PHASE:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(5), async ct =>
                        {
                            if(await Connection.ConnectAsync())
                                ConnectionPhase = RRF_ConnectionPhase.DISCONNECTING_CLIENT;
                        });
                        break;

                    case RRF_ConnectionPhase.DISCONNECTING_CLIENT:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(5), async ct =>
                        {
                            var request = new RRF_Request("rr_disconnect", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
                            var disconnected = await Connection.ExecuteAsync(request);
                            ConnectionPhase = disconnected.Ok ? RRF_ConnectionPhase.CONNECTING_CLIENT : RRF_ConnectionPhase.START_PHASE;
                        });
                        break;

                    case RRF_ConnectionPhase.CONNECTING_CLIENT:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(5), async ct =>
                        {
                            var request = new RRF_Request($"rr_connect?password=\"\"&time={DateTime.UtcNow:O}", HttpMethod.Get, RRF_RequestPriority.Immediate, ct);
                            var connected = await Connection.ExecuteAsync(request);
                            ConnectionPhase = connected.Ok ? RRF_ConnectionPhase.READING_STATUS : RRF_ConnectionPhase.START_PHASE;
                        });
                        break;

                    case RRF_ConnectionPhase.READING_STATUS:
                        Flux.Messages.Messages.Clear();
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(1), async ct =>
                        {
                            var state = await MemoryBuffer.GetModelDataAsync(m => m.State, ct);
                            if (!state.HasValue)
                                return;
                            var status = state.Value.GetProcessStatus();
                            if (!status.HasValue)
                                return;
                            if (status.Value == FLUX_ProcessStatus.CYCLE)
                                ConnectionPhase = RRF_ConnectionPhase.READING_MEMORY;
                            else
                                ConnectionPhase = RRF_ConnectionPhase.CREATING_VARIABLES;
                        });
                        break;

                    case RRF_ConnectionPhase.CREATING_VARIABLES:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(5), async ct =>
                        {
                            var result = await Connection.CreateVariablesAsync(ct);
                            if (result)
                                ConnectionPhase = RRF_ConnectionPhase.INITIALIZING_VARIABLES;
                        });
                        break;

                    case RRF_ConnectionPhase.INITIALIZING_VARIABLES:
                        await CreateTimeoutAsync(TimeSpan.FromSeconds(5), async ct =>
                        {
                            var result = await Connection.InitializeVariablesAsync(ct);
                            if (result)
                                ConnectionPhase = RRF_ConnectionPhase.READING_MEMORY;
                        });
                        break;

                    case RRF_ConnectionPhase.READING_MEMORY:
                        if (MemoryBuffer.HasFullMemoryRead)
                            ConnectionPhase = RRF_ConnectionPhase.END_PHASE;
                        break;

                    case RRF_ConnectionPhase.END_PHASE:
                        break;
                }
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
            }
        }

        public override async Task<bool> ResetClampAsync()
        {
            using var put_reset_clamp_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var wait_reset_clamp_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return await ExecuteParamacroAsync(c => new[]
            {
                "G28 C0",
                "T-1 P0"
            }, put_reset_clamp_cts.Token, true, wait_reset_clamp_cts.Token);
        }
        private static async Task CreateTimeoutAsync(TimeSpan timeout, Func<CancellationToken, Task> func)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeout);
                await func(cts.Token);
            }
            catch
            {
            }
        }

        public override InnerQueueGCodes GenerateInnerQueue(string folder, Job job, MCodePartProgram part_program)
        {
            var gcode = new RRF_GCodeGenerator(Connection);

            var base_motor = VariableStore.ExtrudersUnits.Values.FirstOrOptional(_ => true);
            if (!base_motor.HasValue)
                return default;

            var end = gcode.CreateFile(folder, "end.g", 
                gcode.LogEvent(job, FluxEventType.End));

            var cancel = gcode.CreateFile(folder, "cancel.g",
                gcode.LogEvent(job, FluxEventType.Cancel));

            var start = gcode.CreateFile(folder, "start.g",
                gcode.LogEvent(job, part_program.IsRecovery ? FluxEventType.Resume : FluxEventType.Start),

                gcode.DeclareGlobalArray<double>(4, out var extrusion),
                extrusion.Foreach((var, i) => var.Write($"move.extruders[{i + base_motor.Value.Address}].position")));

            var t0_temp = GetArrayUnit(c => c.TEMP_TOOL, "T1");
            if (!t0_temp.HasValue)
                throw new Exception("");

            var pause = gcode.CreateFile(folder, "pause.g", 
                gcode.LogEvent(job, FluxEventType.Pause),
                
                gcode.DeclareLocalVariable<double>($"heat.heaters[max(state.currentTool + {t0_temp.Value.Address}, 0)].active", out var hold_temp),
                gcode.DeclareLocalVariable<ushort>("state.currentTool", out var hold_tool),
                    
                gcode.DeclareLocalVariable<string>($"0:/{StoragePath}/{job.MCodeKey}.\"^{{{hold_tool}}}^\".\"^{{{hold_temp}}}", out var temp_path),
                gcode.DeclareLocalVariable<string>($"0:/{StoragePath}/recovery.temp", out var recovery_path),

                gcode.CreateFile(StoragePath, "recovery.temp", ""),
                gcode.RenameFile($"{{{temp_path}}}", $"{{{recovery_path}}}"));

            var spin = gcode.CreateFile(folder, "spin.g",
                gcode.DeclareLocalVariable<double>(0.0, out var extrusion_diff),
                gcode.DeclareLocalVariable<double>(0.0, out var extrusion_pos),

                extrusion.Foreach((e, i) =>
                {
                    return extrusion_key((ushort)i).Convert(k => new[]
                    {
                        $"; extrusion {i}",
                        extrusion_pos.Write($"move.extruders[{i + base_motor.Value.Address}].position"),
                        extrusion_diff.Write($"{extrusion_pos} - {e}"),
                        $"if {extrusion_diff} > 0",
                            e.Write($"{extrusion_pos}").Pad(4),
                            gcode.LogExtrusion(job, k, extrusion_diff).Pad(4),
                    });
                }));

            return new InnerQueueGCodes()
            {
                End = end,
                Spin = spin,
                Start = start,
                Pause = pause,
                Cancel = cancel,
            };

            Optional<ExtrusionKey> extrusion_key(ushort position)
            {
                return Flux.Feeders.Feeders.Lookup(position)
                    .Convert(f => f.SelectedMaterial)
                    .Convert(m => m.ExtrusionKey);
            }
        }

        public override GCodeString GenerateStartMCodeLines(MCode mcode)
        {
            var gcode = new RRF_GCodeGenerator(Connection);
            return gcode.CreateString(
                gcode.DeclareLocalVariable<string>($"0:/{InnerQueuePath}/\"^{{global.queue_pos}}^\"/start.g", out var start_path),
                gcode.ExecuteParamacro(start_path));
        }
        public override GCodeString GenerateEndMCodeLines(MCode mcode, Optional<ushort> queue_size)
        {
            var has_unload = queue_size.HasValue && queue_size.Value > 1;

            var gcode = new RRF_GCodeGenerator(Connection);
            return gcode.CreateString(

                gcode.DeclareLocalVariable<string>($"0:/{InnerQueuePath}/\"^{{global.queue_pos}}^\"/end.g", out var end_macro),
                gcode.DeclareLocalVariable<string>(has_unload ? "0:/macros/end_print" : "0:/macros/unload_print", out var next_macro),

                gcode.ExecuteParamacro(end_macro),
                gcode.ExecuteParamacro(next_macro));
        }
    }
}
