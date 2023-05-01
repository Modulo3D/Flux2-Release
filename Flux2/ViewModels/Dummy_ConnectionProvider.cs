using DynamicData.Kernel;
using Modulo3DNet;
using System;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public enum Dummy_ConnectionPhase
    {
        Start,
        End
    }
    public class Dummy_ConnectionProvider : FLUX_ConnectionProvider<Dummy_ConnectionProvider, Dummy_Connection, Dummy_MemoryBuffer, Dummy_VariableStore, Dummy_ConnectionPhase>
    {
        public FluxViewModel Flux { get; }
        public Dummy_ConnectionProvider(FluxViewModel flux) : base(flux,
            Dummy_ConnectionPhase.Start, Dummy_ConnectionPhase.End, p => (int)p,
            c => new Dummy_VariableStore(c),
            c => new Dummy_Connection(c),
            c => new Dummy_MemoryBuffer(c))
        {
            Flux = flux;
        }
        public override Task<bool> ResetClampAsync() => Task.FromResult(false);
        protected override Task RollConnectionAsync(CancellationToken ct) => Task.CompletedTask;

        public override Optional<FluxJobRecovery> GetFluxJobRecoveryFromSource(FluxJob current_job, string source)
        {
            throw new NotImplementedException();
        }
        public override Optional<DateTime> ParseDateTime(string date_time)
        {
            throw new NotImplementedException();
        }

        protected override (GCodeString start_compare, GCodeString end_compare) CompareQueuePosGCode(int queue_pos)
        {
            throw new NotImplementedException();
        }
    }

    public class Dummy_Array<TRData, TWData> : FLUX_Array<TRData, TWData>
    {
        public override string Group => "";
        public Dummy_Array() : base("")
        {
        }
    }
    public class Dummy_Variable<TRData, TWData> : FLUX_Variable<Dummy_Variable<TRData, TWData>, TRData, TWData>
    {
        public override bool ReadOnly => false;
        public override string Group => "";
        public Dummy_Variable() : base("", new VariableUnit(0))
        {
        }
        public override Task<Optional<TRData>> ReadAsync()
        {
            return Task.FromResult(default(Optional<TRData>));
        }
        public override Task<bool> WriteAsync(TWData data)
        {
            return Task.FromResult(false);
        }
    }

    public class Dummy_VariableStore : FLUX_VariableStore<Dummy_VariableStore, Dummy_ConnectionProvider>
    {
        public override bool HasPrintUnloader => throw new NotImplementedException();
        public Dummy_VariableStore(Dummy_ConnectionProvider connection_provider) : base(connection_provider)
        {
            CreateDummy(s => s.AXIS_ENDSTOP);
            CreateDummy(s => s.ENABLE_DRIVERS);
            CreateDummy(s => s.PROGRESS);
            CreateDummy(s => s.TOOL_NUM);
            CreateDummy(s => s.QUEUE);
            CreateDummy(s => s.TOOL_CUR);
            CreateDummy(s => s.PROCESS_STATUS);
            CreateDummy(s => s.IS_HOMED);
            CreateDummy(s => s.DEBUG);
            CreateDummy(s => s.QUEUE_POS);
            CreateDummy(s => s.X_USER_OFFSET);
            CreateDummy(s => s.Y_USER_OFFSET);
            CreateDummy(s => s.Z_USER_OFFSET);
            CreateDummy(s => s.X_PROBE_OFFSET);
            CreateDummy(s => s.Y_PROBE_OFFSET);
            CreateDummy(s => s.Z_PROBE_OFFSET);
            CreateDummy(s => s.X_HOME_OFFSET);
            CreateDummy(s => s.Y_HOME_OFFSET);
            CreateDummy(s => s.TEMP_TOOL);
        }

        public override char FeederAxis => throw new NotImplementedException();
        public override ushort ArrayBase => throw new NotImplementedException();
        public override bool CanProbeMagazine => throw new NotImplementedException();
        public override bool CanMeshProbePlate => throw new NotImplementedException();
        public override bool ParkToolAfterOperation => throw new NotImplementedException();

        public override FLUX_AxisTransform MoveTransform => throw new NotImplementedException();

        public override bool HasMovementLimits => throw new NotImplementedException();

        private void CreateDummy<TRData, TWData>(Expression<Func<Dummy_VariableStore, IFLUX_Array<TRData, TWData>>> array_expression)
        {
            var array_setter = this.GetCachedSetterDelegate(array_expression);
            array_setter.Invoke(new Dummy_Array<TRData, TWData>());
        }
        private void CreateDummy<TRData, TWData>(Expression<Func<Dummy_VariableStore, IFLUX_Variable<TRData, TWData>>> variable_expression)
        {
            var array_setter = this.GetCachedSetterDelegate(variable_expression);
            array_setter.Invoke(new Dummy_Variable<TRData, TWData>());
        }
    }
    public class Dummy_Connection : FLUX_Connection<Dummy_Connection, Dummy_ConnectionProvider, Dummy_VariableStore, IDisposable>
    {
        public override string RootPath => throw new NotImplementedException();
        public override string QueuePath => throw new NotImplementedException();
        public override string MacroPath => throw new NotImplementedException();
        public override string EventPath => throw new NotImplementedException();
        public override string StoragePath => throw new NotImplementedException();
        public override string JobEventPath => throw new NotImplementedException();
        public override string InnerQueuePath => throw new NotImplementedException();
        public override string ExtrusionEventPath => throw new NotImplementedException();


        public Dummy_Connection(Dummy_ConnectionProvider connection_provider) : base(connection_provider)
        {
        }

        public override Task<bool> ClearFolderAsync(string folder, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> DeleteAsync(string folder, string filename, CancellationToken ct)
        {
            throw new NotImplementedException();
        }


        public override GCodeString GetWriteExtrusionMMGCode(ArrayIndex position, double distance_mm)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetHomingGCode(params char[] axis)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetLowerPlateGCode()
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetParkToolGCode()
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetProbePlateGCode()
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetProbeToolGCode(ArrayIndex position, double temperature)
        {
            throw new NotImplementedException();
        }
        public override GCodeString GetRaisePlateGCode()
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetSelectToolGCode(ArrayIndex position)
        {
            throw new NotImplementedException();
        }


        public override Task<Optional<FLUX_FileList>> ListFilesAsync(string folder, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override Task<Optional<string>> GetFileAsync(string folder, string filename, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> CreateFolderAsync(string folder, string name, CancellationToken ct)
        {
            throw new NotImplementedException();
        }


        public override GCodeString GetSetToolOffsetGCode(ArrayIndex position, double x, double y, double z)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> PutFileAsync(
            string folder,
            string filename,
            bool is_paramacro,
            CancellationToken ct,
            GCodeString source = default,
            GCodeString start = default,
            GCodeString end = default,
            Optional<BlockNumber> source_blocks = default,
            Action<double> report_progress = null)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> RenameAsync(string folder, string old_filename, string new_filename, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetSetLowCurrentGCode()
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetProbeMagazineGCode()
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetCancelLoadFilamentGCode(ArrayIndex position)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetCancelUnloadFilamentGCode(ArrayIndex position)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetCenterPositionGCode()
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetSetExtruderMixingGCode(ArrayIndex machine_extruder, ArrayIndex mixing_extruder)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetManualCalibrationPositionGCode()
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetExecuteMacroGCode(string folder, string filename)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> ConnectAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> CloseAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<Stream> GetFileStreamAsync(string folder, string name, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> PutFileStreamAsync(string folder, string name, Stream data, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override string CombinePaths(params string[] paths)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> StopAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> PauseAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> ExecuteParamacroAsync(GCodeString paramacro, CancellationToken put_ct, bool can_cancel = false)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetExitPartProgramGCode()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> CancelAsync()
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetFilamentSensorSettingsGCode(ArrayIndex position, bool enabled)
        {
            throw new NotImplementedException();
        }

        protected override GCodeString GetSetToolTemperatureGCodeInner(ArrayIndex position, double temperature, bool wait)
        {
            throw new NotImplementedException();
        }

        protected override GCodeString GetSetPlateTemperatureGCodeInner(ArrayIndex position, double temperature, bool wait)
        {
            throw new NotImplementedException();
        }

        protected override GCodeString GetSetChamberTemperatureGCodeInner(ArrayIndex position, double temperature, bool wait)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetStartPartProgramGCode(string folder, string filename, Optional<FluxJobRecovery> recovery)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> InitializeVariablesAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetMovementGCode(FLUX_AxisMove axis_move, FLUX_AxisTransform transform)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetResetPositionGCode(FLUX_AxisPosition axis_position, FLUX_AxisTransform transform)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetWriteCurrentJobGCode(Optional<JobKey> job_key)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetLogEventGCode(FluxJob job, FluxEventType event_type)
        {
            throw new NotImplementedException();
        }
        

        public override GCodeString GetDeleteFileGCode(string folder, string filename)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetWriteExtrusionKeyGCode(ArrayIndex position, Optional<ExtrusionKey> extr_key)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetLogExtrusionGCode(ArrayIndex position, Optional<ExtrusionKey> extr_key, FluxJob job)
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetGotoMaintenancePositionGCode()
        {
            throw new NotImplementedException();
        }

        public override GCodeString GetPausePrependGCode(Optional<JobKey> job_key)
        {
            throw new NotImplementedException();
        }
    }
    public class Dummy_MemoryBuffer : FLUX_MemoryBuffer<Dummy_MemoryBuffer, Dummy_ConnectionProvider, Dummy_VariableStore>
    {
        public override Dummy_ConnectionProvider ConnectionProvider { get; }
        public override bool HasFullMemoryRead => false;
        public Dummy_MemoryBuffer(Dummy_ConnectionProvider connection_provider)
        {
            ConnectionProvider = connection_provider;
        }

        public override void Initialize(Dummy_VariableStore variableStore)
        {
        }
    }
}