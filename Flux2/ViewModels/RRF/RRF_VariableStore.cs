﻿using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class RRF_VariableStore : FLUX_VariableStore<RRF_VariableStore>
    {
        public RRF_VariableStore(RRF_ConnectionProvider connection_provider)
        {
            var endstops_unit = new VariableUnit[] { "X", "Y" };
            var axes_unit = new VariableUnit[] { "X", "Y", "Z", "C" };
            var positions_unit = new VariableUnit[] { "X", "Y", "Z", "E" };
            var drivers_unit = new VariableUnit[] { "C", "X", "Y", "Z", "E" };

            var connection = connection_provider.WhenAnyValue(v => v.Connection);
            var heat = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Heat));
            var input = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Inputs));
            var move = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Move));
            var sensors = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Sensors));
            var state = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.State));
            var tools = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Tools));
            var global = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Global));
            var storage = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Storage));
            var queue = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Queue));
            var job = RRF_StateBuilder.Create(connection, m => m.WhenAnyValue(m => m.Job));

            var state_input = RRF_StateBuilder.Create(connection,
                m => Observable.CombineLatest(state.GetState(m), input.GetState(m),
                (s, i) => s.Convert(s => i.Convert(i => (s, i)))));

            var storage_queue = RRF_StateBuilder.Create(connection,
                m => Observable.CombineLatest(storage.GetState(m), queue.GetState(m),
                (s, q) => s.Convert(s => q.Convert(q => (s, q)))));

            var job_global_storage_queue = RRF_StateBuilder.Create(connection,
                m => Observable.CombineLatest(job.GetState(m), global.GetState(m), storage.GetState(m), queue.GetState(m),
                (job, global, storage, queue) => job.Convert(job => global.Convert(global => storage.Convert(storage => queue.Convert(queue => (job, global, storage, queue)))))));

            HOLD_PP = RegisterVariable(new RRF_VariableGlobalModel<string>(connection, "hold_pp", true));
            HOLD_TOOL = RegisterVariable(new RRF_VariableGlobalModel<short>(connection, "hold_tool", true));
            HOLD_TEMP = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "hold_temp", 4, true));
            HOLD_BLK_NUM = RegisterVariable(new RRF_VariableGlobalModel<double>(connection, "hold_blk_num", true));

            QUEUE = RegisterVariable(queue.CreateVariable<Dictionary<ushort, Guid>, Unit>("QUEUE", (c, m) => m.GetGuidDictionaryFromQueue()));
            STORAGE = RegisterVariable(storage.CreateVariable<Dictionary<Guid, MCodePartProgram>, Unit>("STORAGE", (c, m) => m.GetPartProgramDictionaryFromStorage()));
            PROCESS_STATUS = RegisterVariable(state.CreateVariable<FLUX_ProcessStatus, Unit>("PROCESS STATUS", (c, m) => m.GetProcessStatus()));

            PART_PROGRAM = RegisterVariable(job_global_storage_queue.CreateVariable<MCodePartProgram, Unit>("PART PROGRAM", (c, m) => m.GetPartProgram()));
            BLOCK_NUM = RegisterVariable(state_input.CreateVariable<uint, Unit>("BLOCK NUM", (c, m) => m.GetBlocNum()));
            IS_HOMED = RegisterVariable(move.CreateVariable<bool, bool>("IS HOMED", (c, m) => m.IsHomed()));

            var axes = move.CreateArray(0, 4, m => m.Axes);
            AXIS_POSITION = RegisterVariable(axes.Create<double, Unit>("AXIS_POSITION", (c, a) => a.MachinePosition, axes_unit));

            QUEUE_SIZE = RegisterVariable(new RRF_VariableGlobalModel<ushort>(connection, "queue_size", true));
            QUEUE_POS = RegisterVariable(new RRF_VariableGlobalModel<short>(connection, "queue_pos", true));
            IS_HOMING = RegisterVariable(new RRF_VariableGlobalModel<bool>(connection, "is_homing", false));
            IN_CHANGE = RegisterVariable(new RRF_VariableGlobalModel<bool>(connection, "in_change", true));
            X_USER_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "x_user_offset", 4, false));
            Y_USER_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "y_user_offset", 4, false));
            Z_USER_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "z_user_offset", 4, false));
            X_PROBE_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "x_probe_offset", 4, false));
            Y_PROBE_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "y_probe_offset", 4, false));
            Z_PROBE_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "z_probe_offset", 4, false));

            TOOL_CUR = RegisterVariable(state.CreateVariable<short, short>("TOOL CUR", (c, s) => s.CurrentTool));
            TOOL_NUM = RegisterVariable(tools.CreateVariable<ushort, ushort>("TOOL NUM", (c, t) => (ushort)t.Count));
            DEBUG = RegisterVariable(new RRF_VariableGlobalModel<bool>(connection, "debug", true));
            KEEP_CHAMBER = RegisterVariable(new RRF_VariableGlobalModel<bool>(connection, "keep_chamber", true));
            KEEP_TOOL = RegisterVariable(new RRF_VariableGlobalModel<bool>(connection, "keep_tool", true));
            HAS_PLATE = RegisterVariable(new RRF_VariableGlobalModel<bool>(connection, "has_plate", true));
            AUTO_FAN = RegisterVariable(new RRF_VariableGlobalModel<bool>(connection, "auto_fan", true));

            var tool_array = state.CreateArray(0, 4, s => 
            {
                if (!s.CurrentTool.HasValue)
                    return default;
                var tool_list = new List<bool>();
                for (ushort i = 0; i < 4; i++)
                    tool_list.Add(s.CurrentTool.Value == i);
                return tool_list.ToOptional();
            });

            MEM_TOOL_ON_TRAILER = RegisterVariable(tool_array.Create<bool, bool>("TOOL ON TRAILER", (c, m) => m));
            MEM_TOOL_IN_MAGAZINE = RegisterVariable(tool_array.Create<bool, bool>("TOOL IN MAGAZINE", (c, m) => !m));

            //MEM_TOOL_ON_TRAILER = RegisterVariable(new RRF_ArrayGlobalModel<bool>(connection, "tool_on_trailer", 4, true));
            //MEM_TOOL_IN_MAGAZINE = RegisterVariable(new RRF_ArrayGlobalModel<bool>(connection, "tool_in_magazine", 4, true));

            PURGE_POSITION = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "purge_position", 2, true, positions_unit));
            HOME_BUMP = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "home_bump", 3, true, positions_unit));
            HOME_OFFSET = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "home_offset", 3, true, positions_unit));

            X_MAGAZINE_POS = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "x_magazine_pos", 4, true));
            Y_MAGAZINE_POS = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "y_magazine_pos", 4, true));

            TEMP_PLATE = RegisterVariable(heat.CreateVariable<FLUX_Temp, double>("TEMP PLATE", (c, m) => m.GetPlateTemperature(), SetPlateTemperature));

            var heaters = heat.CreateArray(1, 4, h => h.Heaters);
            TEMP_TOOL = RegisterVariable(heaters.Create<FLUX_Temp, double>("TEMP_TOOL", (c, m) => m.GetTemperature(), SetToolTemperatureAsync));

            var endstops = sensors.CreateArray(0, 2, s => s.Endstops);
            AXIS_ENDSTOP = RegisterVariable(endstops.Create<bool, bool>("AXIS ENDSTOP", (c, e) => e.Triggered, custom_unit: endstops_unit));
            if (AXIS_ENDSTOP.HasValue && AXIS_ENDSTOP.Value is RRF_ArrayObjectModel<RRF_ObjectModelSensors, bool, bool> axis_endstop)
            {
                var z_probe = sensors.CreateVariable<bool, bool>("AXIS_ENDSTOP Z", (c, s) => s.GetProbeLevels(0).Convert(l => l[0] > 0));
                axis_endstop.Variables.AddOrUpdate(z_probe);
            }

            ENABLE_DRIVERS = RegisterVariable(axes.Create<bool, bool>("ENABLE DRIVERS", (c, m) => m.IsEnabledDriver(), EnableDriverAsync, drivers_unit));
        }

        public Task<bool> SetPlateTemperature(RRF_Connection connection, double temperature)
        {
            return connection.PostGCodeAsync($"M140 S{temperature}", false, TimeSpan.FromSeconds(5));
        }
        private Task<bool> EnableDriverAsync(RRF_Connection connection, bool enable, (string name, VariableUnit unit, ushort pos) array)
        {
            return connection.PostGCodeAsync($"{(enable ? "M17" : "M18")} {array.unit}", false, TimeSpan.FromSeconds(5));
        }
        public Task<bool> SetToolTemperatureAsync(RRF_Connection connection, double temperature, (string name, VariableUnit unit, ushort pos) array)
        {
            return connection.PostGCodeAsync($"M104 T{array.pos - 1} S{temperature}", false, TimeSpan.FromSeconds(5));
        }
    }
}
