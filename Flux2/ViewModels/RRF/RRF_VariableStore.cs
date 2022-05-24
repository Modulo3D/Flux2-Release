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

        public RRF_VariableStoreBase(RRF_ConnectionProvider connection_provider) : base(connection_provider)
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
        }

        protected Task<bool> SetPlateTemperature(RRF_Connection connection, double temperature, VariableUnit unit)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return connection.PostGCodeAsync($"M140 S{temperature}", cts.Token);
        }
        protected Task<bool> SetChamberTemperature(RRF_Connection connection, double temperature, VariableUnit unit)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return connection.PostGCodeAsync($"M141 P{unit.Index} S{temperature}", cts.Token);
        }
        protected Task<bool> EnableDriverAsync(RRF_Connection connection, bool enable, VariableUnit unit)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return connection.PostGCodeAsync($"{(enable ? "M17" : "M18")} {unit}", cts.Token);
        }
        protected Task<bool> SetToolTemperatureAsync(RRF_Connection connection, double temperature, VariableUnit unit)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return connection.PostGCodeAsync($"M104 T{unit.Index} S{temperature}", cts.Token);
        }
        protected Task<bool> WriteGpOutAsync(RRF_Connection connection, bool pwm, VariableUnit unit)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return connection.PostGCodeAsync($"M42 P{unit.Index} S{(pwm ? 1 : 0)}", cts.Token);
        }
    }

    // S300
    /*public class RRF_VariableStoreS300 : RRF_VariableStoreBase<RRF_VariableStoreS300>
    {
        public RRF_VariableStoreS300(RRF_ConnectionProvider connection_provider) : base(connection_provider)
        {
            try
            {
                var connection = connection_provider.WhenAnyValue(v => v.Connection);

                QUEUE = RegisterVariable(Queue.CreateVariable<Dictionary<QueuePosition, Guid>, Unit>("QUEUE", (c, m) => m.GetGuidDictionaryFromQueue()));
                PROCESS_STATUS = RegisterVariable(State.CreateVariable<FLUX_ProcessStatus, Unit>("PROCESS STATUS", (c, m) => m.GetProcessStatus()));
                STORAGE = RegisterVariable(Storage.CreateVariable<Dictionary<Guid, Dictionary<BlockNumber, MCodePartProgram>>, Unit>("STORAGE", (c, m) => m.GetPartProgramDictionaryFromStorage()));

                PART_PROGRAM = RegisterVariable(PartProgram.CreateVariable<MCodePartProgram, Unit>("PART PROGRAM", (c, m) => m.GetPartProgram()));
                BLOCK_NUM = RegisterVariable(BlockNum.CreateVariable<LineNumber, Unit>("BLOCK NUM", (c, m) => m.GetBlockNum()));
                IS_HOMED = RegisterVariable(Move.CreateVariable<bool, bool>("IS HOMED", (c, m) => m.IsHomed()));

                var axes = Move.CreateArray(m => m.Axes.Convert(a => a.ToDictionary(a => a.Letter)));
                AXIS_POSITION = RegisterVariable(axes.Create<double, Unit>("AXIS_POSITION", VariableUnit.Range("X", "Y", "Z", "C"), (c, a) => a.MachinePosition));

                X_USER_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "x_user_offset", VariableUnit.Range(0, 4), false));
                Y_USER_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "y_user_offset", VariableUnit.Range(0, 4), false));
                Z_USER_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "z_user_offset", VariableUnit.Range(0, 4), false));
                X_PROBE_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "x_probe_offset", VariableUnit.Range(0, 4), false));
                Y_PROBE_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "y_probe_offset", VariableUnit.Range(0, 4), false));
                Z_PROBE_OFFSET_T = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "z_probe_offset", VariableUnit.Range(0, 4), false));
                QUEUE_POS = RegisterVariable(new RRF_VariableGlobalModel<QueuePosition>(connection, "queue_pos", false, new QueuePosition(-1), v => new QueuePosition((short)Convert.ChangeType(v, typeof(short)))));

                TOOL_CUR = RegisterVariable(State.CreateVariable<short, short>("TOOL CUR", (c, s) => s.CurrentTool));
                TOOL_NUM = RegisterVariable(Tools.CreateVariable<ushort, ushort>("TOOL NUM", (c, t) => (ushort)t.Count));
                DEBUG = RegisterVariable(new RRF_VariableGlobalModel<bool>(connection, "debug", false));

                MCODE_RECOVERY = RegisterVariable(Storage.CreateVariable<IFLUX_MCodeRecovery, Unit>("mcode_recovery", GetMCodeRecoveryAsync));

                var tool_array = State.CreateArray(s =>
                {
                    if (!s.CurrentTool.HasValue)
                        return default;
                    var tool_list = new Dictionary<VariableUnit, bool>();
                    for (ushort i = 0; i < 4; i++)
                        tool_list.Add($"{i}", s.CurrentTool.Value == i);
                    return tool_list.ToOptional();
                });

                MEM_TOOL_ON_TRAILER = RegisterVariable(tool_array.Create<bool, bool>("TOOL ON TRAILER", VariableUnit.Range(0, 4), (c, m) => m));
                MEM_TOOL_IN_MAGAZINE = RegisterVariable(tool_array.Create<bool, bool>("TOOL IN MAGAZINE", VariableUnit.Range(0, 4), (c, m) => !m));

                //MEM_TOOL_ON_TRAILER = RegisterVariable(new RRF_ArrayGlobalModel<bool>(connection, "tool_on_trailer", 4, true));
                //MEM_TOOL_IN_MAGAZINE = RegisterVariable(new RRF_ArrayGlobalModel<bool>(connection, "tool_in_magazine", 4, true));

                PURGE_POSITION = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "purge_position", VariableUnit.Range("X", "Y"), true));
                HOME_OFFSET = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "home_offset", VariableUnit.Range("X", "Y", "Z"), true));
                HOME_BUMP = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "home_bump", VariableUnit.Range("X", "Y", "Z"), true));
                QUEUE_SIZE = RegisterVariable(new RRF_VariableGlobalModel<ushort>(connection, "queue_size", true));

                X_MAGAZINE_POS = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "x_magazine_pos", VariableUnit.Range(0, 4), true));
                Y_MAGAZINE_POS = RegisterVariable(new RRF_ArrayGlobalModel<double>(connection, "y_magazine_pos", VariableUnit.Range(0, 4), true));

                TEMP_PLATE = RegisterVariable(Heat.CreateVariable<FLUX_Temp, double>("TEMP PLATE", (c, m) => m.GetPlateTemperature(), SetPlateTemperature));

                var heaters = Heat.CreateArray(h => h.Heaters.Convert(h => h.Skip(1).ToDictionary(h => h.Sensor.Convert(s => s - 1))));
                TEMP_TOOL = RegisterVariable(heaters.Create<FLUX_Temp, double>("TEMP_TOOL", VariableUnit.Range(0, 4), (c, m) => m.GetTemperature(), SetToolTemperatureAsync));

                var endstops = Sensors.CreateArray(s =>
                {
                    return new Dictionary<VariableUnit, bool>
                    {
                        { "X", s.Endstops.Value[0].Triggered.Value },
                        { "Y", s.Endstops.Value[1].Triggered.Value },
                        { "Z", s.GetProbeLevels(0).ConvertOr(l => l[0] > 0, () => false)},
                    }.ToOptional();
                });

                AXIS_ENDSTOP = RegisterVariable(endstops.Create<bool, bool>("AXIS ENDSTOP", VariableUnit.Range("X", "Y", "Z"), (c, triggered) => triggered));
                ENABLE_DRIVERS = RegisterVariable(axes.Create<bool, bool>("ENABLE DRIVERS", VariableUnit.Range("X", "Y", "Z", "C"), (c, m) => m.IsEnabledDriver(), EnableDriverAsync));

                var material_mixing = Tools.CreateArray(tools =>
                {
                    var material_mixing = new Dictionary<VariableUnit, double>();
                    var ordered_tools = tools.OrderBy(t => t.Number.ValueOr(() => 0));
                    foreach (var tool in ordered_tools)
                    {
                        if (!tool.Mix.HasValue)
                            continue;
                        var tool_number = tool.Number.ValueOr(() => 0);
                        foreach (var mixing in tool.Mix.Value.Select((value, index) => (value, index)))
                            material_mixing.Add($"{tool_number + (mixing.index * tools.Count)}", mixing.value);
                    }
                    return material_mixing.ToOptional();
                });

                MATERIAL_ENABLED = RegisterVariable(material_mixing.Create<bool, bool>("MATERIAL ENABLED", VariableUnit.Range(0, 4), (c, m) => m == 1));

                var extruders = Move.CreateArray(m => m.Extruders.Convert(e => e.Select((e, i) => (e, $"{i}")).ToDictionary(e => e.Item2, e => e.e)));
                EXTRUSIONS = RegisterVariable(extruders.Create<double, Unit>("EXTRUSION_SET", VariableUnit.Range(0, 4), (c, e) => e.Position));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

    }*/


    // MP500
    public class RRF_VariableStoreMP500 : RRF_VariableStoreBase<RRF_VariableStoreMP500>
    {
        public RRF_VariableStoreMP500(RRF_ConnectionProvider connection_provider) : base(connection_provider)
        {
            try
            {
                var heater_variables = new HashSet<VariableUnit>()
                {
                    new VariableUnit("bed",     0),
                    new VariableUnit("main",    0),
                    new VariableUnit("spools",  1),
                    new VariableUnit("T0",      0),
                    new VariableUnit("T1",      1) 
                };

                var extruder_units      = VariableUnit.Range(0, "T0", "T1");
                var extruders_variables = VariableUnit.Range(0, "T0", "T1");
                var gpin_variables      = VariableUnit.Range(0, "top", "chamber", "spools");
                var axes_variables      = VariableUnit.Range(0, "X0", "Y0", "Z", "X1", "Y1");
                var endstops_variables  = VariableUnit.Range(0, "X0", "Y0", "Z", "X1", "Y1");
                var gpout_variables     = VariableUnit.Range(0, "chamber", "spools", "light", "vacuum");
                var analog_variables    = VariableUnit.Range(0, "bed", "chamber", "spools", "T0", "T1", "vacuum");
                var filament_variables  = VariableUnit.Range(1, 10);

                var connection          = connection_provider.WhenAnyValue(v => v.Connection);
                var gpIn                = Sensors.CreateArray(s     => s.GpIn,              gpin_variables);
                var axes                = Move.CreateArray(m        => m.Axes,              axes_variables);
                var gpOut               = State.CreateArray(s       => s.GpOut,             gpout_variables);
                var heaters             = Heat.CreateArray(h        => h.Heaters,           heater_variables);
                var analog              = Sensors.CreateArray(s     => s.Analog,            analog_variables);
                var endstops            = Sensors.CreateArray(s     => s.Endstops,          endstops_variables);
                var extruders           = Move.CreateArray(m        => m.Extruders,         extruders_variables);
                var filaments           = Sensors.CreateArray(m     => m.FilamentMonitors,  filament_variables);
            
                TOOL_NUM                = Tools.CreateVariable<ushort, ushort>("TOOL NUM",          (c, t) => (ushort)t.Count);

                TOOL_CUR                = State.CreateVariable<short, short>("TOOL CUR",            (c, s) => s.CurrentTool);
                PROCESS_STATUS          = State.CreateVariable("PROCESS STATUS",                    (c, m) => m.GetProcessStatus());
 
                QUEUE                   = Queue.CreateVariable("QUEUE",                             (c, m) => m.GetGuidDictionaryFromQueue());

                STORAGE                 = Storage.CreateVariable("STORAGE",                         (c, m) => m.GetPartProgramDictionaryFromStorage());
                MCODE_RECOVERY          = Storage.CreateVariable("mcode_recovery",                  (c, f) => f.GetMCodeRecoveryAsync(c));

                PART_PROGRAM            = PartProgram.CreateVariable("PART PROGRAM",                (c, m) => m.GetPartProgram());
                BLOCK_NUM               = BlockNum.CreateVariable("BLOCK NUM",                      (c, m) => m.GetBlockNum());
                IS_HOMED                = Move.CreateVariable<bool, bool>("IS HOMED",               (c, m) => m.IsHomed());

                AXIS_POSITION           = axes.CreateArray("AXIS_POSITION",                         (c, a) => a.MachinePosition);                
                AXIS_ENDSTOP            = endstops.CreateArray<bool, bool>("AXIS ENDSTOP",          (c, e) => e.Triggered);
                EXTRUSIONS              = extruders.CreateArray("EXTRUSION_SET",                    (c, e) => e.Position);
                ENABLE_DRIVERS          = axes.CreateArray<bool, bool>("ENABLE DRIVERS",            (c, m) => m.IsEnabledDriver(), EnableDriverAsync);
                LOCK_CLOSED             = gpIn.CreateArray<bool, bool>("LOCK_CLOSED",               (c, m) => m.Value == 1, VariableRange.Range(1, 1));


                VACUUM_PRESENCE         = analog.CreateVariable("ANALOG",                           (c, v) => AnalogSensors.PSE541.Read(v.LastReading),     "vacuum");
                TEMP_PLATE              = heaters.CreateVariable<FLUX_Temp, double>("TEMP",         (c, m) => m.GetTemperature(), SetPlateTemperature,      "bed");
                TEMP_CHAMBER            = heaters.CreateArray<FLUX_Temp, double>("TEMP_CHAMBER",    (c, m) => m.GetTemperature(), SetChamberTemperature,    VariableRange.Range(1, 2));
                TEMP_TOOL               = heaters.CreateArray<FLUX_Temp, double>("TEMP_TOOL",       (c, m) => m.GetTemperature(), SetToolTemperatureAsync,  VariableRange.Range(3, 2));

                CHAMBER_LIGHT           = gpOut.CreateVariable<bool, bool>("ENABLE",                (c, m) => m.Pwm == 1, WriteGpOutAsync, "light");
                ENABLE_VACUUM           = gpOut.CreateVariable<bool, bool>("ENABLE",                (c, m) => m.Pwm == 1, WriteGpOutAsync, "vacuum");
                OPEN_LOCK               = gpOut.CreateArray<bool, bool>("OPEN_LOCK",                (c, m) => m.Pwm == 1, WriteGpOutAsync, VariableRange.Range(0, 2));

                FILAMENT_BEFORE_GEAR    = filaments.CreateArray<bool, bool>("FILAMENT_BEFORE_GEAR", (c, m) => m.Status == "ok", VariableRange.Range(0, 4));
                FILAMENT_AFTER_GEAR     = filaments.CreateArray<bool, bool>("FILAMENT_AFTER_GEAR",  (c, m) => m.Status == "ok", VariableRange.Range(4, 4));
                FILAMENT_ON_HEAD        = filaments.CreateArray<bool, bool>("FILAMENT_ON_HEAD",     (c, m) => m.Status == "ok", VariableRange.Range(8, 2));


                VACUUM_LEVEL            = Global.CreateVariable("vacuum_level",     true,   -70.0);
                X_USER_OFFSET_T         = Global.CreateArray("x_user_offset",       false,  0.0,    VariableUnit.Range(0, 2));
                Y_USER_OFFSET_T         = Global.CreateArray("y_user_offset",       false,  0.0,    VariableUnit.Range(0, 2));
                Z_USER_OFFSET_T         = Global.CreateArray("z_user_offset",       false,  0.0,    VariableUnit.Range(0, 2));
                X_PROBE_OFFSET_T        = Global.CreateArray("x_probe_offset",      false,  0.0,    VariableUnit.Range(0, 2));
                Y_PROBE_OFFSET_T        = Global.CreateArray("y_probe_offset",      false,  0.0,    VariableUnit.Range(0, 2));
                Z_PROBE_OFFSET_T        = Global.CreateArray("z_probe_offset",      false,  0.0,    VariableUnit.Range(0, 2));
                PURGE_POSITION          = Global.CreateArray("purge_position",      true,   0.0,    VariableUnit.Range(0, "X", "Y"));
                HOME_OFFSET             = Global.CreateArray("home_offset",         true,   0.0,    VariableUnit.Range(0, "X", "Y", "Z"));
            
                QUEUE_SIZE              = Global.CreateVariable("queue_size",       true,   (ushort)1);
                DEBUG                   = Global.CreateVariable("debug",            false,  false);
                QUEUE_POS               = Global.CreateVariable("queue_pos",        false,  new QueuePosition(-1), v => new QueuePosition((short)Convert.ChangeType(v, typeof(short))));

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

    }
}
