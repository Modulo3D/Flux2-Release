using DynamicData;
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
        public RRF_VariableGlobalModel<bool> ITERATOR { get; }

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
     
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<RRF_ObjectModelMove>.RRF_ArrayBuilder<RRF_ObjectModelAxis> Axes { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<RRF_ObjectModelSensors>.RRF_ArrayBuilder<RRF_ObjectModelGpIn> GpIn { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<RRF_ObjectModelState>.RRF_ArrayBuilder<RRF_ObjectModelGpOut> GpOut { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<RRF_ObjectModelHeat>.RRF_ArrayBuilder<RRF_ObjectModelHeater> Heaters { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<RRF_ObjectModelSensors>.RRF_ArrayBuilder<RRF_ObjectModelAnalog> Analog { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<RRF_ObjectModelSensors>.RRF_ArrayBuilder<RRF_FilamentMonitor> Filaments { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<RRF_ObjectModelMove>.RRF_ArrayBuilder<RRF_ObjectModelExtruder> Extruders { get; }
        public RRF_ModelBuilder<TRRF_VariableStore>.RRF_InnerModelBuilder<RRF_ObjectModelSensors>.RRF_ArrayBuilder<RRF_ObjectModelEndstop> Endstops { get; }

        public abstract Dictionary<VariableAlias, VariableUnit> AxesUnits { get; }
        public abstract Dictionary<VariableAlias, VariableUnit> GpInUnits { get; }
        public abstract Dictionary<VariableAlias, VariableUnit> GpOutUnits { get; }
        public abstract Dictionary<VariableAlias, VariableUnit> HeaterUnits { get; }
        public abstract Dictionary<VariableAlias, VariableUnit> AnalogUnits { get; }
        public abstract Dictionary<VariableAlias, VariableUnit> ExtrudersUnits { get; }
        public abstract Dictionary<VariableAlias, VariableUnit> EndstopsUnits { get; }
        public abstract Dictionary<VariableAlias, VariableUnit> FilamentUnits { get; }

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
                Queue = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Queue, read_timeout); // TODO
                Inputs = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Inputs, read_timeout);
                Storage = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Storage, read_timeout); // TODO
                Sensors = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Sensors, read_timeout);
                BlockNum = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Job, m => m.State, m => m.Inputs, read_timeout);
                PartProgram = RRF_ModelBuilder<TRRF_VariableStore>.CreateModel(this, m => m.Job, m => m.Global, m => m.Storage, m => m.Queue, read_timeout);

                var extruders_units = VariableUnit.Range(0, max_extruders);

                var extruders = Move.CreateArray(m => m.Extruders, extruders_units);
                EXTRUSIONS = extruders.CreateArray("EXTRUSIONS", (c, e) => e.Position);

                Axes                    = Move.CreateArray(m => m.Axes, AxesUnits);
                GpIn                    = Sensors.CreateArray(s => s.GpIn, GpInUnits);
                GpOut                   = State.CreateArray(s => s.GpOut, GpOutUnits);
                Heaters                 = Heat.CreateArray(h => h.Heaters, HeaterUnits);
                Analog                  = Sensors.CreateArray(s => s.Analog, AnalogUnits);
                Extruders               = Move.CreateArray(m => m.Extruders, ExtrudersUnits);
                Endstops                = Sensors.CreateArray(s => s.Endstops, EndstopsUnits);
                Filaments               = Sensors.CreateArray(m => m.FilamentMonitors, FilamentUnits);

                AXIS_POSITION           = Axes.CreateArray("AXIS_POSITION", (c, a) => a.MachinePosition);
                AXIS_ENDSTOP            = Endstops.CreateArray("AXIS ENDSTOP", (c, e) => e.Triggered);
                ENABLE_DRIVERS          = Axes.CreateArray<bool, bool>("ENABLE DRIVERS", (c, m) => m.IsEnabledDriver(), EnableDriverAsync);

                PROGRESS                = Job.CreateVariable("PROGRESS", (c, m) => m.GetParamacroProgress());
                STORAGE                 = Storage.CreateVariable("STORAGE", (c, m) => m.GetPartProgramDictionaryFromStorage());
                MCODE_RECOVERY          = Storage.CreateVariable("mcode_recovery", (c, f) => f.GetMCodeRecoveryAsync(c));
                TOOL_NUM                = Tools.CreateVariable<ushort, ushort>("TOOL NUM", (c, t) => (ushort)t.Count);
                PART_PROGRAM            = PartProgram.CreateVariable("PART PROGRAM", (c, m) => m.GetPartProgram());
                QUEUE                   = Queue.CreateVariable("QUEUE", (c, m) => m.GetJobDictionaryFromQueue());
                TOOL_CUR                = State.CreateVariable<short, short>("TOOL CUR", (c, s) => s.CurrentTool);
                PROCESS_STATUS          = State.CreateVariable("PROCESS STATUS", (c, m) => m.GetProcessStatus());
                IS_HOMED                = Move.CreateVariable<bool, bool>("IS HOMED", (c, m) => m.IsHomed());

                ITERATOR                = Global.CreateVariable("iterator",             false,  true);
                KEEP_CHAMBER            = Global.CreateVariable("keep_chamber",         true,   false);
                KEEP_TOOL               = Global.CreateVariable("keep_tool",            false,  false);
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
    public class RRF_VariableStore : RRF_VariableStoreBase<RRF_VariableStore>
    {
        public override Dictionary<VariableAlias, VariableUnit> AxesUnits => VariableUnit.Range(0, "X", "Y", "Z", /*"U",*/ "C");
        public override Dictionary<VariableAlias, VariableUnit> GpInUnits => VariableUnit.Range(0, 0);
        public override Dictionary<VariableAlias, VariableUnit> GpOutUnits => VariableUnit.Range(0, 0);
        public override Dictionary<VariableAlias, VariableUnit> HeaterUnits => new[]
        {
            new VariableUnit("bed",     0, 0),
            new VariableUnit("T1",      1, 0),
            new VariableUnit("T2",      2, 1),
            new VariableUnit("T3",      3, 2),
            new VariableUnit("T4",      4, 3),
        }.ToDictionary(u => u.Alias);
        public override Dictionary<VariableAlias, VariableUnit> AnalogUnits => VariableUnit.Range(0, "bed", "T1", "T2", "T3", "T4");
        public override Dictionary<VariableAlias, VariableUnit> ExtrudersUnits => VariableUnit.Range(0, "T1", "T2", "T3", "T4");
        public override Dictionary<VariableAlias, VariableUnit> EndstopsUnits => VariableUnit.Range(0, "X", "Y", "Z", "U");
        public override Dictionary<VariableAlias, VariableUnit> FilamentUnits => VariableUnit.Range(0, 0);

        public RRF_VariableStore(RRF_ConnectionProvider connection_provider) : base(connection_provider, 4)
        {
            try
            {
                var tool_array = State.CreateArray(s =>
                {
                    if (!s.CurrentTool.HasValue)
                        return default;
                    var tool_list = new List<bool>();
                    for (ushort i = 0; i < 4; i++)
                        tool_list.Add(s.CurrentTool.Value == i);
                    return tool_list.ToOptional();
                }, VariableUnit.Range(0, 4));

                TEMP_PLATE              = Heaters.CreateVariable<FLUX_Temp, double>("TEMP", (c, m) => m.GetTemperature(), SetPlateTemperature, "bed");
                TEMP_TOOL               = Heaters.CreateArray<FLUX_Temp, double>("TEMP_TOOL", (c, m) => m.GetTemperature(), SetToolTemperatureAsync, VariableRange.Range(1, 4));

                MEM_TOOL_ON_TRAILER     = tool_array.CreateArray<bool, bool>("TOOL ON TRAILER",    (c, m) => m);
                MEM_TOOL_IN_MAGAZINE    = tool_array.CreateArray<bool, bool>("TOOL IN MAGAZINE",   (c, m) => !m);   

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
    /*public class RRF_VariableStore : RRF_VariableStoreBase<RRF_VariableStore>
    {
        public override Dictionary<VariableAlias, VariableUnit> AxesUnits => VariableUnit.Range(0, "X", "Y", "Z", "U", "V");
        public override Dictionary<VariableAlias, VariableUnit> GpInUnits => VariableUnit.Range(0, "chamber", "spools", "M1", "M2", "M3", "M4", "T1", "T2");
        public override Dictionary<VariableAlias, VariableUnit> GpOutUnits => VariableUnit.Range(0, "chamber", "spools", "light", "vacuum");
        public override Dictionary<VariableAlias, VariableUnit> HeaterUnits => new[]
        {
            new VariableUnit("bed",     0, 0),
            new VariableUnit("main",    1, 0),
            new VariableUnit("spools",  2, 1),
            new VariableUnit("T1",      3, 0),
            new VariableUnit("T2",      4, 1)
        }.ToDictionary(u => u.Alias);
        public override Dictionary<VariableAlias, VariableUnit> AnalogUnits => VariableUnit.Range(0, "bed", "chamber", "spools", "T1", "T2", "vacuum");
        public override Dictionary<VariableAlias, VariableUnit> ExtrudersUnits => VariableUnit.Range(0, "T1", "T2");
        public override Dictionary<VariableAlias, VariableUnit> EndstopsUnits => VariableUnit.Range(0, "X", "Y", "Z", "U", "V");
        public override Dictionary<VariableAlias, VariableUnit> FilamentUnits => VariableUnit.Range(2, "M1", "M2", "M3", "M4");
        public RRF_VariableStore(RRF_ConnectionProvider connection_provider) : base(connection_provider, 2)
        {
            try
            {
                VACUUM_PRESENCE         = Analog.CreateVariable("ANALOG",                           (c, v) => AnalogSensors.PSE541.Read(v.LastReading),     "vacuum");
                TEMP_PLATE              = Heaters.CreateVariable<FLUX_Temp, double>("TEMP",         (c, m) => m.GetTemperature(), SetPlateTemperature,      "bed");
                TEMP_CHAMBER            = Heaters.CreateArray<FLUX_Temp, double>("TEMP_CHAMBER",    (c, m) => m.GetTemperature(), SetChamberTemperature,    VariableRange.Range(1, 2));
                TEMP_TOOL               = Heaters.CreateArray<FLUX_Temp, double>("TEMP_TOOL",       (c, m) => m.GetTemperature(), SetToolTemperatureAsync,  VariableRange.Range(3, 2));

                CHAMBER_LIGHT           = GpOut.CreateVariable<bool, bool>("ENABLE",                (c, m) => m.Pwm == 1, WriteGpOutAsync, "light");
                ENABLE_VACUUM           = GpOut.CreateVariable<bool, bool>("ENABLE",                (c, m) => m.Pwm == 1, WriteGpOutAsync, "vacuum");
                OPEN_LOCK               = GpOut.CreateArray<bool, bool>("OPEN_LOCK",                (c, m) => m.Pwm == 1, WriteGpOutAsync, VariableRange.Range(0, 2));
                
                LOCK_CLOSED             = GpIn.CreateArray<bool, bool>("LOCK_CLOSED",               (c, m) => m.Value == 1, VariableRange.Range(0, 2));
                FILAMENT_AFTER_GEAR     = GpIn.CreateArray<bool, bool>("FILAMENT_AFTER_GEAR",       (c, m) => m.Value == 1, VariableRange.Range(2, 4));
                FILAMENT_ON_HEAD        = GpIn.CreateArray<bool, bool>("FILAMENT_ON_HEAD",          (c, m) => m.Value == 1, VariableRange.Range(6, 2));

                FILAMENT_BEFORE_GEAR    = Filaments.CreateArray<bool, bool>("FILAMENT_BEFORE_GEAR", (c, m) => m.Status == "ok");
                
                Z_PROBE_CORRECTION      = Global.CreateVariable("z_probe_correction",   true,   0.0);
                VACUUM_LEVEL            = Global.CreateVariable("vacuum_level",         true,   -70.0);
                Z_BED_HEIGHT            = Global.CreateVariable("z_bed_height",         false,  FluxViewModel.MaxZBedHeight);  
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }*/
}
