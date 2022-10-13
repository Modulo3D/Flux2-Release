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
        public IFLUX_Variable<bool, bool> ITERATOR { get; set; }

        public RRF_GlobalModelBuilder.RRF_InnerGlobalModelBuilder Global{ get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<FLUX_FileList> JobEvents { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<FLUX_FileList> Extrusions { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelJob> Job { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelHeat> Heat { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelMove> Move { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelState> State { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelSensors> Sensors { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<List<RRF_ObjectModelTool>> Tools { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<List<RRF_ObjectModelInput>> Inputs { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<(FLUX_FileList queue, FLUX_FileList storage)> JobQueue { get; }

        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelMove>.RRF_ArrayBuilder<RRF_ObjectModelAxis> Axes { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelSensors>.RRF_ArrayBuilder<RRF_ObjectModelGpIn> GpIn { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelState>.RRF_ArrayBuilder<RRF_ObjectModelGpOut> GpOut { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelHeat>.RRF_ArrayBuilder<RRF_ObjectModelHeater> Heaters { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelSensors>.RRF_ArrayBuilder<RRF_ObjectModelAnalog> Analog { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelSensors>.RRF_ArrayBuilder<RRF_FilamentMonitor> Filaments { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelMove>.RRF_ArrayBuilder<RRF_ObjectModelExtruder> Extruders { get; }
        public RRF_ModelBuilder.RRF_InnerModelBuilder<RRF_ObjectModelSensors>.RRF_ArrayBuilder<RRF_ObjectModelEndstop> Endstops { get; }

        public virtual VariableUnits AxesUnits { get; }
        public virtual VariableUnits GpInUnits { get; }
        public virtual VariableUnits GpOutUnits { get; }
        public virtual VariableUnits HeaterUnits { get; }
        public virtual VariableUnits AnalogUnits { get; }
        public virtual VariableUnits ExtrudersUnits { get; }
        public virtual VariableUnits EndstopsUnits { get; }
        public virtual VariableUnits FilamentUnits { get; }

        public RRF_VariableStoreBase(RRF_ConnectionProvider connection_provider, ushort max_extruders) : base(connection_provider)
        {     
            try
            { 
                var read_timeout        = TimeSpan.FromSeconds(5);

                Global                  = RRF_GlobalModelBuilder.CreateModel(this);
                Job                     = RRF_ModelBuilder.CreateModel(this, m => m.Job, read_timeout);
                Heat                    = RRF_ModelBuilder.CreateModel(this, m => m.Heat, read_timeout);
                Move                    = RRF_ModelBuilder.CreateModel(this, m => m.Move, read_timeout);
                State                   = RRF_ModelBuilder.CreateModel(this, m => m.State, read_timeout);
                Tools                   = RRF_ModelBuilder.CreateModel(this, m => m.Tools, read_timeout);
                Inputs                  = RRF_ModelBuilder.CreateModel(this, m => m.Inputs, read_timeout);
                Sensors                 = RRF_ModelBuilder.CreateModel(this, m => m.Sensors, read_timeout);
                JobEvents               = RRF_ModelBuilder.CreateModel(this, m => m.JobEvents, read_timeout);
                Extrusions              = RRF_ModelBuilder.CreateModel(this, m => m.Extrusions, read_timeout);
                JobQueue                = RRF_ModelBuilder.CreateModel(this, m => m.Queue, m => m.Storage, read_timeout);

                Axes                    = Move.CreateArray(m => m.Axes, AxesUnits);
                GpIn                    = Sensors.CreateArray(s => s.GpIn, GpInUnits);
                GpOut                   = State.CreateArray(s => s.GpOut, GpOutUnits);
                Heaters                 = Heat.CreateArray(h => h.Heaters, HeaterUnits);
                Analog                  = Sensors.CreateArray(s => s.Analog, AnalogUnits);
                Extruders               = Move.CreateArray(m => m.Extruders, ExtrudersUnits);
                Endstops                = Sensors.CreateArray(s => s.Endstops, EndstopsUnits);
                Filaments               = Sensors.CreateArray(m => m.FilamentMonitors, FilamentUnits);
                
                Axes.CreateArray(c => c.AXIS_POSITION,          (c, a) => a.MachinePosition);
                Axes.CreateArray(c => c.ENABLE_DRIVERS,         (c, m) => m.IsEnabledDriver(), EnableDriverAsync);
                
                Endstops.CreateArray(c => c.AXIS_ENDSTOP,       (c, e) => e.Triggered, (c, e, u) => Task.FromResult(true)); 

                JobQueue.CreateVariable(c => c.JOB_QUEUE,       (c, m) => FLUX_FileList.GetJobQueue(m.queue, m.storage));
                Extrusions.CreateVariable(c => c.EXTRUSIONS,    (c, m) => m.GetExtrusionSetQueue());
                Job.CreateVariable(c => c.PROGRESS,             (c, m) => m.GetParamacroProgress());
                JobEvents.CreateVariable(c => c.MCODE_EVENT,    (c, m) => m.GetMCodeEvents());
                Tools.CreateVariable(c => c.TOOL_NUM,           (c, t) => (ushort)t.Count);
                
                State.CreateVariable(c => c.TOOL_CUR,           (c, s) => s.CurrentTool.Convert(t => (ArrayIndex)(t, connection_provider)));
                State.CreateVariable(c => c.PROCESS_STATUS,     (c, m) => m.GetProcessStatus());
                State.CreateVariable(c => c.IN_CHANGE,          (c, m) => m.IsInChange());
                
                Move.CreateVariable(c => c.IS_HOMED,            (c, m) => m.IsHomed());

                Global.CreateVariable(c => c.ITERATOR,      false,  true);
                Global.CreateVariable(c => c.RUN_DAEMON,    false,  true);
                Global.CreateVariable(c => c.KEEP_CHAMBER,  true,   false);
                Global.CreateVariable(c => c.KEEP_TOOL,     false,  false);
                Global.CreateVariable(c => c.DEBUG,         false,  false);
                Global.CreateVariable(c => c.QUEUE_SIZE,    true,   (ushort)1);
                Global.CreateVariable(c => c.QUEUE_POS,     false,  new QueuePosition(-1), v => new QueuePosition((short)Convert.ChangeType(v, typeof(short))));

                Global.CreateArray(c => c.X_USER_OFFSET,    false,  0.0, max_extruders);
                Global.CreateArray(c => c.Y_USER_OFFSET,    false,  0.0, max_extruders);
                Global.CreateArray(c => c.Z_USER_OFFSET,    false,  0.0, max_extruders);
                Global.CreateArray(c => c.X_PROBE_OFFSET,   false,  0.0, max_extruders);
                Global.CreateArray(c => c.Y_PROBE_OFFSET,   false,  0.0, max_extruders);
                Global.CreateArray(c => c.Z_PROBE_OFFSET,   false,  0.0, max_extruders);
                Global.CreateArray(c => c.X_HOME_OFFSET,    true,   0.0, max_extruders);
                Global.CreateArray(c => c.Y_HOME_OFFSET,    true,   0.0, max_extruders);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected async Task<bool> SetPlateTemperatureAsync(RRF_ConnectionProvider connection_provider, double temperature, VariableUnit unit)
        {
            var connection = connection_provider.Connection;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return await connection.PostGCodeAsync(new[] { $"M140 S{temperature}" }, cts.Token);
        }
        protected async Task<bool> SetChamberTemperatureAsync(RRF_ConnectionProvider connection_provider, double temperature, VariableUnit unit)
        {
            var connection = connection_provider.Connection;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return await connection.PostGCodeAsync(new[] { $"M141 P{unit.Index} S{temperature}" }, cts.Token);
        }
        protected async Task<bool> EnableDriverAsync(RRF_ConnectionProvider connection_provider, bool enable, VariableUnit unit)
        {
            var connection = connection_provider.Connection;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return await connection.PostGCodeAsync(new[] { $"{(enable ? "M17" : "M18")} {unit.Alias}" }, cts.Token);
        }
        protected async Task<bool> SetToolTemperatureAsync(RRF_ConnectionProvider connection_provider, double temperature, VariableUnit unit)
        {
            var connection = connection_provider.Connection;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return await connection.PostGCodeAsync(new[] { $"M104 T{unit.Index} S{temperature}" }, cts.Token);
        }
        protected async Task<bool> WriteGpOutAsync(RRF_ConnectionProvider connection_provider, bool pwm, VariableUnit unit)
        {
            var connection = connection_provider.Connection;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return await connection.PostGCodeAsync(new[] { $"M42 P{unit.Address} S{(pwm ? 1 : 0)}" }, cts.Token);
        }
    }

    // S300
    public class RRF_VariableStoreS300 : RRF_VariableStoreBase
    {
        public override VariableUnits ExtrudersUnits    => new(("T", 4));

        public override VariableUnits HeaterUnits       => new("main.plate", ("T", 4));
        public override VariableUnits AnalogUnits       => new("main.plate", ("T", 4));

        public override VariableUnits EndstopsUnits     => new("X", "Y", "Z");
        public override VariableUnits AxesUnits         => new("X", "Y", "Z", "C");

        public override VariableUnits GpInUnits         => new("clamp");

        public RRF_VariableStoreS300(RRF_ConnectionProvider connection_provider) : base(connection_provider, 4)
        {
            try
            {
                var read_timeout = TimeSpan.FromSeconds(5);


                GpIn.CreateVariable(c => c.OPEN_HEAD_CLAMP, (c, g) => g.Value == 1, "clamp");

                var tool_array_model = RRF_ModelBuilder.CreateModel(this, m => m.State, m => m.Sensors, read_timeout);
                var tool_array = tool_array_model.CreateArray(s =>
                {
                    var current_tool = s.Item1.CurrentTool;
                    if (!current_tool.HasValue)
                        return default;

                    var tool_presence = s.Item2.GpIn.Convert(g => g[0].Value == 0);
                    if (!tool_presence.HasValue)
                        return default;

                    var tool_list = new List<(ushort position, short selected_tool, bool tool_presence)>();
                    for (ushort i = 0; i < 4; i++)
                        tool_list.Add((i, current_tool.Value, tool_presence.Value));
                    
                    return tool_list.ToOptional();
                }, new("T", 4));

                Heaters.CreateArray(c =>    c.TEMP_PLATE,           (c, m) => m.GetTemperature(), SetPlateTemperatureAsync, "main.plate");
                Heaters.CreateArray(c =>    c.TEMP_TOOL,            (c, m) => m.GetTemperature(), SetToolTemperatureAsync,  ("T", 4));

                tool_array.CreateArray(c => c.MEM_TOOL_ON_TRAILER,  (c, m) => m.selected_tool > -1 && (m.selected_tool == m.position && m.tool_presence));
                tool_array.CreateArray(c => c.MEM_TOOL_IN_MAGAZINE, (c, m) => m.selected_tool > -1 ? m.selected_tool != m.position : !m.tool_presence);   

                Global.CreateArray(c =>     c.X_MAGAZINE_POS, true, 0.0, 4);
                Global.CreateArray(c =>     c.Y_MAGAZINE_POS, true, 0.0, 4);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
    public class RRF_VariableStoreS300C : RRF_VariableStoreS300
    {
        public RRF_VariableStoreS300C(RRF_ConnectionProvider connection_provider) : base(connection_provider)
        { 
        }
    }
    public class RRF_VariableStoreS300A : RRF_VariableStoreS300
    {
        public override VariableUnits EndstopsUnits => new("X", "Y", "Z", "U");
        public override VariableUnits AxesUnits => new("X", "Y", "Z", "B", "C", "U");
        public RRF_VariableStoreS300A(RRF_ConnectionProvider connection_provider) : base(connection_provider)
        {
        }
    }

    // MP500
    public class RRF_VariableStoreMP500 : RRF_VariableStoreBase
    {
        public override VariableUnits ExtrudersUnits    => new(("T", 2));
        public override VariableUnits AxesUnits         => new("X", "Y", "Z", "U", "V");
        public override VariableUnits EndstopsUnits     => new("X", "Y", "Z", "U", "V");
        
        public override VariableUnits FilamentUnits     => new(2, ("M", 4));
        public override VariableUnits GpInUnits         => new("main.lock", "spools.lock", ("M", 4), ("T", 2));
        public override VariableUnits GpOutUnits        => new("main.lock", "spools.lock", "main.light", "main.vacuum", "shutdown");

        public override VariableUnits HeaterUnits       => new("main.plate", "main.chamber", ("T", 2));
        public override VariableUnits AnalogUnits       => new("main.plate", "main.chamber", ("T", 2), "main.vacuum", "filament", ("H", 1));

        public RRF_VariableStoreMP500(RRF_ConnectionProvider connection_provider) : base(connection_provider, 2)
        {
            try
            {
                Heaters.CreateArray(c =>    c.TEMP_CHAMBER,         (c, m) => m.GetTemperature(),   SetChamberTemperatureAsync, "main.chamber");
                Heaters.CreateArray(c =>    c.TEMP_TOOL,            (c, m) => m.GetTemperature(),   SetToolTemperatureAsync,    ("T", 2));
                Heaters.CreateArray(c =>    c.TEMP_PLATE,           (c, m) => m.GetTemperature(),   SetPlateTemperatureAsync,   "main.plate");

                GpOut.CreateArray(c =>      c.OPEN_LOCK,            (c, m) => m.Pwm == 1,           WriteGpOutAsync,            "main.lock", "spools.lock");
                GpOut.CreateVariable(c =>   c.CHAMBER_LIGHT,        (c, m) => m.Pwm == 1,           WriteGpOutAsync,            "main.light");
                GpOut.CreateVariable(c =>   c.ENABLE_VACUUM,        (c, m) => m.Pwm == 1,           WriteGpOutAsync,            "main.vacuum");
                GpOut.CreateVariable(c =>   c.DISABLE_24V,          (c, m) => m.Pwm == 1,           WriteGpOutAsync,            "shutdown");

                Analog.CreateVariable(c =>  c.VACUUM_PRESENCE,      (c, v) => read_pressure(v),     "main.vacuum");
                GpIn.CreateArray(c =>       c.LOCK_CLOSED,          (c, m) => m.Value == 1,         "main.lock", "spools.lock");
                GpIn.CreateArray(c =>       c.FILAMENT_AFTER_GEAR,  (c, m) => m.Value == 1,         ("M", 4));
                GpIn.CreateArray(c =>       c.FILAMENT_ON_HEAD,     (c, m) => m.Value == 1,         ("T", 2));
                Analog.CreateArray(c =>     c.FILAMENT_HUMIDITY,    (c, h) => read_humidity(h),     "H");

                Filaments.CreateArray(c => c.FILAMENT_BEFORE_GEAR,  (c, m) => m.Status == "ok", ("M", 4));
                
                Global.CreateVariable(c => c.Z_BED_HEIGHT,          false,  FluxViewModel.MaxZBedHeight);  
                Global.CreateVariable(c => c.Z_PROBE_CORRECTION,    true,   0.0);
                Global.CreateVariable(c => c.VACUUM_LEVEL,          true,   -70.0);

                Optional<Pressure> read_pressure(RRF_ObjectModelAnalog value) => AnalogSensors.PSE541.Instance.Read(value.LastReading);
                Optional<FLUX_Humidity> read_humidity(RRF_ObjectModelAnalog value) => value.LastReading.Convert(r => new FLUX_Humidity(r));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
