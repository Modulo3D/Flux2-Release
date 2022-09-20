﻿using DynamicData;
using DynamicData.Kernel;
using Modulo3DStandard;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class Dummy_ConnectionProvider : FLUX_ConnectionProvider<Dummy_ConnectionProvider, Dummy_Connection, Dummy_MemoryBuffer, Dummy_VariableStore>
    {
        public FluxViewModel Flux { get; }
        public override IFlux IFlux => Flux;
        public override double ConnectionProgress => 0;
        public override Optional<bool> IsConnecting => true;
        public override Optional<bool> IsInitializing => true;
        public override Dummy_MemoryBuffer MemoryBuffer { get; }
        public override Dummy_VariableStore VariableStore { get; }

        public Dummy_ConnectionProvider(FluxViewModel flux)
        {
            Flux = flux;
            MemoryBuffer = new Dummy_MemoryBuffer(this);
            VariableStore = new Dummy_VariableStore(this);
        }

        public override void Initialize()
        {
        }
        public override void StartConnection()
        {
        }
        protected override Task RollConnectionAsync() => Task.CompletedTask;
        public override Task<bool> ParkToolAsync() => Task.FromResult(false);
        public override Task<bool> ResetClampAsync() => Task.FromResult(false);
        public override Optional<IEnumerable<string>> GenerateEndMCodeLines(MCode mcode, Optional<ushort> queue_size) => default;

        public override Optional<IEnumerable<string>> GenerateStartMCodeLines(MCode mcode)
        {
            throw new NotImplementedException();
        }
    }

    public class Dummy_Array<TRData, TWData> : FLUX_Array<TRData, TWData>
    {
        public override string Group => "";
        public override Optional<VariableUnit> GetArrayUnit(ushort position) => default;
        public Dummy_Array() : base("")
        {
        }
    }
    public class Dummy_Variable<TRData, TWData> : FLUX_Variable<TRData, TWData>
    {
        public override bool ReadOnly => false;
        public override string Group => "";
        public Dummy_Variable() : base("", new VariableUnit(0))
        {
        }
        public override Task<Optional<TRData>> ReadAsync()
        {
            return Task.FromResult(Optional<TRData>.None);
        }
        public override Task<bool> WriteAsync(TWData data)
        {
            return Task.FromResult(false);
        }
    }

    public class Dummy_VariableStore : FLUX_VariableStore<Dummy_VariableStore, Dummy_ConnectionProvider>
    {
        public Dummy_VariableStore(Dummy_ConnectionProvider connection_provider) : base(connection_provider)
        {
            CreateDummy(s => s.AXIS_ENDSTOP);
            CreateDummy(s => s.ENABLE_DRIVERS);
            CreateDummy(s => s.PROGRESS);
            CreateDummy(s => s.STORAGE);
            CreateDummy(s => s.MCODE_RECOVERY);
            CreateDummy(s => s.TOOL_NUM);
            CreateDummy(s => s.PART_PROGRAM);
            CreateDummy(s => s.QUEUE);
            CreateDummy(s => s.TOOL_CUR);
            CreateDummy(s => s.PROCESS_STATUS);
            CreateDummy(s => s.IS_HOMED);
            CreateDummy(s => s.DEBUG);
            CreateDummy(s => s.QUEUE_SIZE);
            CreateDummy(s => s.QUEUE_POS);
            CreateDummy(s => s.X_USER_OFFSET);
            CreateDummy(s => s.Y_USER_OFFSET);
            CreateDummy(s => s.Z_USER_OFFSET);
            CreateDummy(s => s.X_PROBE_OFFSET);
            CreateDummy(s => s.Y_PROBE_OFFSET);
            CreateDummy(s => s.Z_PROBE_OFFSET);
            CreateDummy(s => s.X_HOME_OFFSET);
            CreateDummy(s => s.Y_HOME_OFFSET);
        }

        private void CreateDummy<TRData, TWData>(Expression<Func<Dummy_VariableStore, IFLUX_Array<TRData, TWData>>> array_expression)
        {
            var array_setter = this.GetCachedSetterDelegate(array_expression);
            array_setter.Invoke(new Dummy_Array<TRData, TWData>());
        }
        private void CreateDummy<TRData, TWData>(Expression<Func<Dummy_VariableStore, IFLUX_Variable<TRData, TWData>>> array_expression)
        {
            var array_setter = this.GetCachedSetterDelegate(array_expression);
            array_setter.Invoke(new Dummy_Variable<TRData, TWData>());
        }
    }
    public class Dummy_Connection : FLUX_Connection<Dummy_ConnectionProvider, Dummy_VariableStore, IDisposable>
    {
        public override string RootPath => throw new NotImplementedException();
        public override string QueuePath => throw new NotImplementedException();
        public override string MacroPath => throw new NotImplementedException();
        public override ushort ArrayBase => throw new NotImplementedException();
        public override string StoragePath => throw new NotImplementedException();
        public override string PathSeparator => throw new NotImplementedException();
        public override string InnerQueuePath => throw new NotImplementedException();

        public Dummy_Connection(Dummy_ConnectionProvider connection_provider) : base(connection_provider, default)
        {
        }

        public override Task<bool> ClearFolderAsync(string folder, bool wait, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> CycleAsync(bool start, bool wait = false, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> DeleteFileAsync(string folder, string filename, bool wait, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> DeselectPartProgramAsync(bool from_drive, bool wait, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetHomingGCode(params char[] axis)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetLowerPlateGCode()
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetParkToolGCode()
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetProbePlateGCode()
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetProbeToolGCode(ushort position, double temperature)
        {
            throw new NotImplementedException();
        }
        public override Optional<IEnumerable<string>> GetRaisePlateGCode()
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetRelativeEMovementGCode(double distance, double feedrate)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetRelativeXMovementGCode(double distance, double feedrate)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetRelativeYMovementGCode(double distance, double feedrate)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetRelativeZMovementGCode(double distance, double feedrate)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetSelectToolGCode(ushort position)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetSetToolTemperatureGCode(ushort position, double temperature)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetStartPartProgramGCode(string folder, string file_name)
        {
            throw new NotImplementedException();
        }
        public override Task<Optional<FLUX_FileList>> ListFilesAsync(string folder, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> ResetAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> SelectPartProgramAsync(string partprogram, bool from_drive, bool wait, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override Task<Optional<string>> DownloadFileAsync(string folder, string filename, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> CreateFolderAsync(string folder, string name, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> HoldAsync()
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetSetToolOffsetGCode(ushort position, double x, double y, double z)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> CancelPrintAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> PutFileAsync(
            string folder,
            string filename,
            bool is_paramacro, 
            CancellationToken ct,
            Optional<IEnumerable<string>> source = default,
            Optional<IEnumerable<string>> start = default,
            Optional<IEnumerable<string>> end = default,
            Optional<uint> source_blocks = default,
            Action<double> report_progress = null)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> RenameFileAsync(string folder, string old_filename, string new_filename, bool wait, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetSetLowCurrentGCode()
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetProbeMagazineGCode()
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetCancelLoadFilamentGCode(ushort position)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetCancelUnloadFilamentGCode(ushort position)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetCenterPositionGCode()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> ExecuteParamacroAsync(IEnumerable<string> paramacro, CancellationToken put_ct, bool wait = false, CancellationToken ct = default, bool can_cancel = false)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetSetExtruderMixingGCode(ushort machine_extruder, ushort mixing_extruder)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetManualCalibrationPositionGCode()
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetExecuteMacroGCode(string folder, string filename)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetCancelOperationGCode()
        {
            throw new NotImplementedException();
        }
    }
    public class Dummy_MemoryBuffer : FLUX_MemoryBuffer<Dummy_ConnectionProvider>
    {
        public override Dummy_ConnectionProvider ConnectionProvider { get; }
        public Dummy_MemoryBuffer(Dummy_ConnectionProvider connection_provider)
        {
            ConnectionProvider = connection_provider;         
        }
    }
}