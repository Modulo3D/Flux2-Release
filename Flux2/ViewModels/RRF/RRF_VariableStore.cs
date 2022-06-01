﻿using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public abstract class RRF_VariableStoreBase<TRRF_VariableStore> : FLUX_VariableStore<TRRF_VariableStore, RRF_ConnectionProvider>
        where TRRF_VariableStore : RRF_VariableStoreBase<TRRF_VariableStore>
    {
        public RRF_GlobalModelBuilder<TRRF_VariableStore>.RRF_InnerGlobalModelBuilder Global{ get; }

        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<FLUX_FileList> Queue { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<FLUX_FileList> Storage { get; }
        
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<RRF_ObjectModelJob> Job { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<RRF_ObjectModelHeat> Heat { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<RRF_ObjectModelMove> Move { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<RRF_ObjectModelState> State { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<RRF_ObjectModelSensors> Sensors { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<List<RRF_ObjectModelTool>> Tools { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<List<RRF_ObjectModelInput>> Inputs { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<(RRF_ObjectModelJob job, RRF_ObjectModelState state, List<RRF_ObjectModelInput> inputs)> BlockNum { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<(RRF_ObjectModelJob job, RRF_ObjectModelGlobal global, FLUX_FileList storage, FLUX_FileList queue)> PartProgram { get; }

        public RRF_VariableStoreBase(RRF_ConnectionProvider connection_provider, ushort max_extruders) : base(connection_provider)
        {     
            try
            { 
                var read_timeout = TimeSpan.FromSeconds(5);
            
                Global = RRF_GlobalModelBuilder<TRRF_VariableStore>.CreateModel(this);
                Job = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Job, read_timeout);
                Heat = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Heat, read_timeout);
                Move = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Move, read_timeout);
                State = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.State, read_timeout);
                Tools = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Tools, read_timeout);
                Queue = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Queue, read_timeout);
                Inputs = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Inputs, read_timeout);
                Storage = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Storage, read_timeout);
                Sensors = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Sensors, read_timeout);
                BlockNum = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Job, m => m.State, m => m.Inputs, read_timeout);
                PartProgram = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Job, m => m.Global, m => m.Storage, m => m.Queue, read_timeout);

                var extruders_units = VariableUnit.Range(0, max_extruders);

                var extruders = Move.CreateArray(m => m.Extruders, extruders_units);
                EXTRUSIONS = extruders.CreateArray("EXTRUSIONS", (c, e) => e.Position);


                STORAGE                 = Storage.CreateVariable("STORAGE", (c, m) => m.GetPartProgramDictionaryFromStorage());
                MCODE_RECOVERY          = Storage.CreateVariable("mcode_recovery", (c, f) => f.GetMCodeRecoveryAsync(c));
                TOOL_NUM                = Tools.CreateVariable<ushort, ushort>("TOOL NUM", (c, t) => (ushort)t.Count);
                PART_PROGRAM            = PartProgram.CreateVariable("PART PROGRAM", (c, m) => m.GetPartProgram());
                QUEUE                   = Queue.CreateVariable("QUEUE", (c, m) => m.GetGuidDictionaryFromQueue());
                TOOL_CUR                = State.CreateVariable<short, short>("TOOL CUR", (c, s) => s.CurrentTool);
                PROCESS_STATUS          = State.CreateVariable("PROCESS STATUS", (c, m) => m.GetProcessStatus());
                IS_HOMED                = Move.CreateVariable<bool, bool>("IS HOMED", (c, m) => m.IsHomed());
                BLOCK_NUM               = BlockNum.CreateVariable("BLOCK NUM", (c, m) => m.GetBlockNum());

                DEBUG                   = Global.CreateVariable("debug",                false,  false);
                QUEUE_SIZE              = Global.CreateVariable("queue_size",           true,   (ushort)1);
                QUEUE_POS               = Global.CreateVariable("queue_pos",            false,  new QueuePosition(-1), v => new QueuePosition((short)Convert.ChangeType(v, typeof(short))));

                X_USER_OFFSET_T         = Global.CreateArray("x_user_offset",           false,  0.0,    VariableUnit.Range(0, max_extruders));
                Y_USER_OFFSET_T         = Global.CreateArray("y_user_offset",           false,  0.0,    VariableUnit.Range(0, max_extruders));
                Z_USER_OFFSET_T         = Global.CreateArray("z_user_offset",           false,  0.0,    VariableUnit.Range(0, max_extruders));
                X_PROBE_OFFSET_T        = Global.CreateArray("x_probe_offset",          false,  0.0,    VariableUnit.Range(0, max_extruders));
                Y_PROBE_OFFSET_T        = Global.CreateArray("y_probe_offset",          false,  0.0,    VariableUnit.Range(0, max_extruders));
                Z_PROBE_OFFSET_T        = Global.CreateArray("z_probe_offset",          false,  0.0,    VariableUnit.Range(0, max_extruders));
                X_HOME_OFFSET           = Global.CreateArray("x_home_offset",           true,   0.0,    VariableUnit.Range(0, max_extruders));
                Y_HOME_OFFSET           = Global.CreateArray("y_home_offset",           true,   0.0,    VariableUnit.Range(0, max_extruders));
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected Task<bool> SetPlateTemperature(RRF_Connection connection, double temperature, VariableUnit unit)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return connection.PostGCodeAsync(new[] { $"M140 S{temperature}" }, cts.Token);
        }
        protected Task<bool> SetChamberTemperature(RRF_Connection connection, double temperature, VariableUnit unit)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return connection.PostGCodeAsync(new[] { $"M141 P{unit.Index} S{temperature}" }, cts.Token);
        }
        protected Task<bool> EnableDriverAsync(RRF_Connection connection, bool enable, VariableUnit unit)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return connection.PostGCodeAsync(new[] { $"{(enable ? "M17" : "M18")} {unit.Alias}" }, cts.Token);
        }
        protected Task<bool> SetToolTemperatureAsync(RRF_Connection connection, double temperature, VariableUnit unit)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return connection.PostGCodeAsync(new[] { $"M104 T{unit.Index} S{temperature}" }, cts.Token);
        }
        protected Task<bool> WriteGpOutAsync(RRF_Connection connection, bool pwm, VariableUnit unit)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return connection.PostGCodeAsync(new[] { $"M42 P{unit.Address} S{(pwm ? 1 : 0)}" }, cts.Token);
        }
    }

    // S300
    public class RRF_VariableStoreS300 : RRF_VariableStoreBase<RRF_VariableStoreS300>
    {
        public RRF_VariableStoreS300(RRF_ConnectionProvider connection_provider) : base(connection_provider, 4)
        {
            try
            {

                var heater_units = new[]
                {
                    new VariableUnit("bed",     0, 0),
                    new VariableUnit("T1",      1, 0),
                    new VariableUnit("T2",      2, 1),
                    new VariableUnit("T3",      3, 2),
                    new VariableUnit("T4",      4, 3),
                }.ToDictionary(u => u.Alias);

                var endstops_units      = VariableUnit.Range(0, "X", "Y", "Z", "U");
                var extruders_units     = VariableUnit.Range(0, "T1", "T2", "T3", "T4");
                var axes_units          = VariableUnit.Range(0, "X", "Y", "Z", "U", "C");
                var analog_units        = VariableUnit.Range(0, "bed", "T1", "T2", "T3", "T4");

                var connection          = connection_provider.WhenAnyValue(v => v.Connection);
                var axes                = Move.CreateArray(m        => m.Axes,              axes_units);
                var heaters             = Heat.CreateArray(h        => h.Heaters,           heater_units);
                var analog              = Sensors.CreateArray(s     => s.Analog,            analog_units);
                var endstops            = Sensors.CreateArray(s     => s.Endstops,          endstops_units);
                var extruders           = Move.CreateArray(m        => m.Extruders,         extruders_units);

                var tool_array = State.CreateArray(s =>
                {
                    if (!s.CurrentTool.HasValue)
                        return default;
                    var tool_list = new List<bool>();
                    for (ushort i = 0; i < 4; i++)
                        tool_list.Add(s.CurrentTool.Value == i);
                    return tool_list.ToOptional();
                }, VariableUnit.Range(0, 4));

                AXIS_POSITION           = axes.CreateArray("AXIS_POSITION", (c, a) => a.MachinePosition);
                AXIS_ENDSTOP            = endstops.CreateArray("AXIS ENDSTOP", (c, e) => e.Triggered);
                ENABLE_DRIVERS          = axes.CreateArray<bool, bool>("ENABLE DRIVERS", (c, m) => m.IsEnabledDriver(), EnableDriverAsync);

                MEM_TOOL_ON_TRAILER     = tool_array.CreateArray<bool, bool>("TOOL ON TRAILER",    (c, m) => m);
                MEM_TOOL_IN_MAGAZINE    = tool_array.CreateArray<bool, bool>("TOOL IN MAGAZINE",   (c, m) => !m);   

                TEMP_PLATE              = heaters.CreateVariable<FLUX_Temp, double>("TEMP",     (c, m) => m.GetTemperature(), SetPlateTemperature, "bed");
                TEMP_TOOL               = heaters.CreateArray<FLUX_Temp, double>("TEMP_TOOL",   (c, m) => m.GetTemperature(), SetToolTemperatureAsync, VariableRange.Range(1, 4));       

                X_MAGAZINE_POS          = Global.CreateArray("x_magazine_pos",          true,   0.0, VariableUnit.Range(0, 4));
                Y_MAGAZINE_POS          = Global.CreateArray("y_magazine_pos",          true,   0.0, VariableUnit.Range(0, 4));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

    }


    // MP500
    public class RRF_VariableStoreMP500 : RRF_VariableStoreBase<RRF_VariableStoreMP500>
    {
        public RRF_VariableStoreMP500(RRF_ConnectionProvider connection_provider) : base(connection_provider, 2)
        {
            try
            {
                var heater_units = new[]
                {
                    new VariableUnit("bed",     0, 0),
                    new VariableUnit("main",    1, 0),
                    new VariableUnit("spools",  2, 1),
                    new VariableUnit("T1",      3, 0),
                    new VariableUnit("T2",      4, 1) 
                }.ToDictionary(u => u.Alias);

                var extruders_units     = VariableUnit.Range(0, "T1", "T2");
                var filament_units      = VariableUnit.Range(2, "M1", "M2", "M3", "M4");
                var axes_units          = VariableUnit.Range(0, "X", "Y", "Z", "U", "V");
                var endstops_units      = VariableUnit.Range(0, "X", "Y", "Z", "U", "V");
                var gpout_units         = VariableUnit.Range(0, "chamber", "spools", "light", "vacuum");
                var analog_units        = VariableUnit.Range(0, "bed", "chamber", "spools", "T1", "T2", "vacuum");
                var gpin_units          = VariableUnit.Range(0, "chamber", "spools", "M1", "M2", "M3", "M4", "T1", "T2" );

                var connection          = connection_provider.WhenAnyValue(v => v.Connection);
                var gpIn                = Sensors.CreateArray(s     => s.GpIn,              gpin_units);
                var axes                = Move.CreateArray(m        => m.Axes,              axes_units);
                var gpOut               = State.CreateArray(s       => s.GpOut,             gpout_units);
                var heaters             = Heat.CreateArray(h        => h.Heaters,           heater_units);
                var analog              = Sensors.CreateArray(s     => s.Analog,            analog_units);
                var endstops            = Sensors.CreateArray(s     => s.Endstops,          endstops_units);
                var extruders           = Move.CreateArray(m        => m.Extruders,         extruders_units);
                var filaments           = Sensors.CreateArray(m     => m.FilamentMonitors,  filament_units);

                AXIS_POSITION           = axes.CreateArray("AXIS_POSITION", (c, a) => a.MachinePosition);
                AXIS_ENDSTOP            = endstops.CreateArray("AXIS ENDSTOP", (c, e) => e.Triggered);
                ENABLE_DRIVERS          = axes.CreateArray<bool, bool>("ENABLE DRIVERS", (c, m) => m.IsEnabledDriver(), EnableDriverAsync);

                VACUUM_PRESENCE         = analog.CreateVariable("ANALOG",                           (c, v) => AnalogSensors.PSE541.Read(v.LastReading),     "vacuum");
                TEMP_PLATE              = heaters.CreateVariable<FLUX_Temp, double>("TEMP",         (c, m) => m.GetTemperature(), SetPlateTemperature,      "bed");
                TEMP_CHAMBER            = heaters.CreateArray<FLUX_Temp, double>("TEMP_CHAMBER",    (c, m) => m.GetTemperature(), SetChamberTemperature,    VariableRange.Range(1, 2));
                TEMP_TOOL               = heaters.CreateArray<FLUX_Temp, double>("TEMP_TOOL",       (c, m) => m.GetTemperature(), SetToolTemperatureAsync,  VariableRange.Range(3, 2));

                CHAMBER_LIGHT           = gpOut.CreateVariable<bool, bool>("ENABLE",                (c, m) => m.Pwm == 1, WriteGpOutAsync, "light");
                ENABLE_VACUUM           = gpOut.CreateVariable<bool, bool>("ENABLE",                (c, m) => m.Pwm == 1, WriteGpOutAsync, "vacuum");
                OPEN_LOCK               = gpOut.CreateArray<bool, bool>("OPEN_LOCK",                (c, m) => m.Pwm == 1, WriteGpOutAsync, VariableRange.Range(0, 2));
                
                LOCK_CLOSED             = gpIn.CreateArray<bool, bool>("LOCK_CLOSED",               (c, m) => m.Value == 1, VariableRange.Range(0, 2));
                FILAMENT_AFTER_GEAR     = gpIn.CreateArray<bool, bool>("FILAMENT_AFTER_GEAR",       (c, m) => m.Value == 1, VariableRange.Range(2, 4));
                FILAMENT_ON_HEAD        = gpIn.CreateArray<bool, bool>("FILAMENT_ON_HEAD",          (c, m) => m.Value == 1, VariableRange.Range(6, 2));

                FILAMENT_BEFORE_GEAR    = filaments.CreateArray<bool, bool>("FILAMENT_BEFORE_GEAR", (c, m) => m.Status == "ok");
                
                Z_PROBE_CORRECTION      = Global.CreateVariable("z_probe_correction",   true,   0.0);
                VACUUM_LEVEL            = Global.CreateVariable("vacuum_level",         true,   -70.0);
                Z_BED_HEIGHT            = Global.CreateVariable("z_bed_height",         false,  FluxViewModel.MaxZBedHeight);       
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

    }
}
