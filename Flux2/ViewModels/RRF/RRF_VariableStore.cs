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
    public abstract class RRF_VariableStoreBase : FLUX_VariableStore<RRF_VariableStoreBase, RRF_ConnectionProvider>
    {
        public IFLUX_Variable<bool, bool> ITERATOR { get; }
        public IFLUX_Variable<bool, bool> RUN_DAEMON { get; }

        public RRF_GlobalModelBuilder.RRF_InnerGlobalModelBuilder Global{ get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<FLUX_FileList> Queue { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<FLUX_FileList> Storage { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelJob> Job { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelHeat> Heat { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelMove> Move { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelState> State { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelSensors> Sensors { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<List<RRF_ObjectModelTool>> Tools { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<List<RRF_ObjectModelInput>> Inputs { get; }

        public RRF_ModelBuilder.RRF_InnerModelBuilder<(RRF_ObjectModelJob job, RRF_ObjectModelState state, List<RRF_ObjectModelInput> inputs)> BlockNum { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<(RRF_ObjectModelJob job, RRF_ObjectModelGlobal global, FLUX_FileList storage, FLUX_FileList queue)> PartProgram { get; }

        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelMove>.RRF_ArrayBuilder<RRF_ObjectModelAxis> Axes { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelSensors>.RRF_ArrayBuilder<RRF_ObjectModelGpIn> GpIn { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelState>.RRF_ArrayBuilder<RRF_ObjectModelGpOut> GpOut { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelHeat>.RRF_ArrayBuilder<RRF_ObjectModelHeater> Heaters { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelSensors>.RRF_ArrayBuilder<RRF_ObjectModelAnalog> Analog { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelSensors>.RRF_ArrayBuilder<RRF_FilamentMonitor> Filaments { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelMove>.RRF_ArrayBuilder<RRF_ObjectModelExtruder> Extruders { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelSensors>.RRF_ArrayBuilder<RRF_ObjectModelEndstop> Endstops { get; }

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
            
                Global = RRF_GlobalModelBuilder.CreateModel(this);
                Job = RRF_ModelBuilder.CreateModel(this, m => m.Job, read_timeout);
                Heat = RRF_ModelBuilder.CreateModel(this, m => m.Heat, read_timeout);
                Move = RRF_ModelBuilder.CreateModel(this, m => m.Move, read_timeout);
                State = RRF_ModelBuilder.CreateModel(this, m => m.State, read_timeout);
                Tools = RRF_ModelBuilder.CreateModel(this, m => m.Tools, read_timeout);
                Queue = RRF_ModelBuilder.CreateModel(this, m => m.Queue, read_timeout); // TODO
                Inputs = RRF_ModelBuilder.CreateModel(this, m => m.Inputs, read_timeout);
                Storage = RRF_ModelBuilder.CreateModel(this, m => m.Storage, read_timeout); // TODO
                Sensors = RRF_ModelBuilder.CreateModel(this, m => m.Sensors, read_timeout);
                BlockNum = RRF_ModelBuilder.CreateModel(this, m => m.Job, m => m.State, m => m.Inputs, read_timeout);
                PartProgram = RRF_ModelBuilder.CreateModel(this, m => m.Job, m => m.Global, m => m.Storage, m => m.Queue, read_timeout);

                var extruders_units = VariableUnit.Range(0, max_extruders);


                Axes                    = Move.CreateArray(m => m.Axes, AxesUnits);
                GpIn                    = Sensors.CreateArray(s => s.GpIn, GpInUnits);
                GpOut                   = State.CreateArray(s => s.GpOut, GpOutUnits);
                Heaters                 = Heat.CreateArray(h => h.Heaters, HeaterUnits);
                Analog                  = Sensors.CreateArray(s => s.Analog, AnalogUnits);
                Extruders               = Move.CreateArray(m => m.Extruders, ExtrudersUnits);
                Endstops                = Sensors.CreateArray(s => s.Endstops, EndstopsUnits);
                Filaments               = Sensors.CreateArray(m => m.FilamentMonitors, FilamentUnits);

                Extruders.CreateArray(c => c.EXTRUSIONS, (c, e) => e.Position);
                
                Axes.CreateArray(c => c.AXIS_POSITION, (c, a) => a.MachinePosition);
                Axes.CreateArray(c => c.ENABLE_DRIVERS, (c, m) => m.IsEnabledDriver(), write_data: EnableDriverAsync);
                
                Endstops.CreateArray(c => c.AXIS_ENDSTOP, (c, e) => e.Triggered, write_data: (c, e, u) => Task.FromResult(true));

                Queue.CreateVariable(c => c.QUEUE,              (c, m) => m.GetJobDictionaryFromQueue());
                Job.CreateVariable(c => c.PROGRESS,             (c, m) => m.GetParamacroProgress());
                PartProgram.CreateVariable(c => c.PART_PROGRAM, (c, m) => m.GetPartProgram());
                Tools.CreateVariable(c => c.TOOL_NUM,           (c, t) => (ushort)t.Count);
                
                Storage.CreateVariable(c => c.STORAGE,          (c, m) => m.GetPartProgramDictionaryFromStorage());
                Storage.CreateVariable(c => c.MCODE_RECOVERY,   (c, f) => f.GetMCodeRecoveryAsync(c));
                
                State.CreateVariable(c => c.PROCESS_STATUS, (c, m) => m.GetProcessStatus());
                State.CreateVariable(c => c.TOOL_CUR, (c, s) => s.CurrentTool);
                
                Move.CreateVariable(c => c.IS_HOMED, (c, m) => m.IsHomed());

                Global.CreateVariable(c => c.ITERATOR,      false,  true);
                Global.CreateVariable(c => c.RUN_DAEMON,    false,  true);
                Global.CreateVariable(c => c.KEEP_CHAMBER,  true,   false);
                Global.CreateVariable(c => c.KEEP_TOOL,     false,  false);
                Global.CreateVariable(c => c.DEBUG,         false,  false);
                Global.CreateVariable(c => c.QUEUE_SIZE,    true,   (ushort)1);
                Global.CreateVariable(c => c.QUEUE_POS,     false,  new QueuePosition(-1), v => new QueuePosition((short)Convert.ChangeType(v, typeof(short))));

                Global.CreateArray(c => c.X_USER_OFFSET_T,  false,  0.0,    VariableUnit.Range(0, max_extruders));
                Global.CreateArray(c => c.Y_USER_OFFSET_T,  false,  0.0,    VariableUnit.Range(0, max_extruders));
                Global.CreateArray(c => c.Z_USER_OFFSET_T,  false,  0.0,    VariableUnit.Range(0, max_extruders));
                Global.CreateArray(c => c.X_PROBE_OFFSET_T, false,  0.0,    VariableUnit.Range(0, max_extruders));
                Global.CreateArray(c => c.Y_PROBE_OFFSET_T, false,  0.0,    VariableUnit.Range(0, max_extruders));
                Global.CreateArray(c => c.Z_PROBE_OFFSET_T, false,  0.0,    VariableUnit.Range(0, max_extruders));
                Global.CreateArray(c => c.X_HOME_OFFSET,    true,   0.0,    VariableUnit.Range(0, max_extruders));
                Global.CreateArray(c => c.Y_HOME_OFFSET,    true,   0.0,    VariableUnit.Range(0, max_extruders));
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
    public class RRF_VariableStoreS300 : RRF_VariableStoreBase
    {
        public override Dictionary<VariableAlias, VariableUnit> AxesUnits => VariableUnit.Range(0, "X", "Y", "Z", "U", "C");
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

        public RRF_VariableStoreS300(RRF_ConnectionProvider connection_provider) : base(connection_provider, 4)
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

                Heaters.CreateVariable(c => c.TEMP_PLATE, (c, m) => m.GetTemperature(), "bed", SetPlateTemperature);
                Heaters.CreateArray(c => c.TEMP_TOOL, (c, m) => m.GetTemperature(), VariableRange.Range(1, 4), SetToolTemperatureAsync);

                tool_array.CreateArray(c => c.MEM_TOOL_ON_TRAILER,    (c, m) => m);
                tool_array.CreateArray(c => c.MEM_TOOL_IN_MAGAZINE,   (c, m) => !m);   

                Global.CreateArray(c => c.X_MAGAZINE_POS, true,   0.0, VariableUnit.Range(0, 4));
                Global.CreateArray(c => c.Y_MAGAZINE_POS, true,   0.0, VariableUnit.Range(0, 4));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }


    // MP500
    public class RRF_VariableStoreMP500 : RRF_VariableStoreBase
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
        public RRF_VariableStoreMP500(RRF_ConnectionProvider connection_provider) : base(connection_provider, 2)
        {
            try
            {
                Heaters.CreateArray(c => c.TEMP_CHAMBER,        (c, m) => m.GetTemperature(), VariableRange.Range(1, 2), SetChamberTemperature);
                Heaters.CreateArray(c => c.TEMP_TOOL,           (c, m) => m.GetTemperature(), VariableRange.Range(3, 2), SetToolTemperatureAsync);
                Heaters.CreateVariable(c => c.TEMP_PLATE,       (c, m) => m.GetTemperature(), "bed", SetPlateTemperature);

                Analog.CreateVariable(c => c.VACUUM_PRESENCE,   (c, v) => AnalogSensors.PSE541.Instance.Read(v.LastReading), "vacuum");

                GpOut.CreateArray(c => c.OPEN_LOCK, (c, m) => m.Pwm == 1, VariableRange.Range(0, 2), WriteGpOutAsync);
                GpOut.CreateVariable(c => c.CHAMBER_LIGHT, (c, m) => m.Pwm == 1, "light", WriteGpOutAsync);
                GpOut.CreateVariable(c => c.ENABLE_VACUUM, (c, m) => m.Pwm == 1, "vacuum", WriteGpOutAsync);
                
                GpIn.CreateArray(c => c.LOCK_CLOSED,            (c, m) => m.Value == 1, VariableRange.Range(0, 2));
                GpIn.CreateArray(c => c.FILAMENT_AFTER_GEAR,    (c, m) => m.Value == 1, VariableRange.Range(2, 4));
                GpIn.CreateArray(c => c.FILAMENT_ON_HEAD,       (c, m) => m.Value == 1, VariableRange.Range(6, 2));

                Filaments.CreateArray(c => c.FILAMENT_BEFORE_GEAR, (c, m) => m.Status == "ok");
                
                Global.CreateVariable(c => c.Z_BED_HEIGHT,          false,  FluxViewModel.MaxZBedHeight);  
                Global.CreateVariable(c => c.Z_PROBE_CORRECTION,    true,   0.0);
                Global.CreateVariable(c => c.VACUUM_LEVEL,          true,   -70.0);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
