using DynamicData.Kernel;
using Modulo3DStandard;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    internal class Dummy_ConnectionProvider : FLUX_ConnectionProvider<Dummy_Connection, Dummy_VariableStore>
    {
        public FluxViewModel Flux { get; }
        public override IFlux IFlux => Flux;
        public override double ConnectionProgress => 0;
        public override Optional<bool> IsConnecting => true;
        public override Optional<bool> IsInitializing => true;
        public Dummy_ConnectionProvider(FluxViewModel flux)
        {
            Flux = flux;
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
    }

    internal class Dummy_VariableStore : FLUX_VariableStore<Dummy_VariableStore>
    {
        public Dummy_ConnectionProvider ConnectionProvider { get; }
        public Dummy_VariableStore(Dummy_ConnectionProvider connectionProvider)
        {
            ConnectionProvider = connectionProvider;
        }
    }
    internal class Dummy_Connection : FLUX_Connection<Dummy_VariableStore, Unit, Dummy_MemoryBuffer>
    {
        public override Dummy_MemoryBuffer MemoryBuffer { get; }

        public override string QueuePath => throw new NotImplementedException();

        public override string InnerQueuePath => throw new NotImplementedException();

        public override string StoragePath => throw new NotImplementedException();

        public Dummy_Connection(Dummy_VariableStore variable_store) : base(variable_store, Unit.Default)
        {
            MemoryBuffer = new Dummy_MemoryBuffer(this);
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

        public override string[] GetGotoPurgePositionGCode(ushort position)
        {
            throw new NotImplementedException();
        }

        public override string[] GetGotoReaderGCode(ushort position)
        {
            throw new NotImplementedException();
        }

        public override string[] GetHomingGCode()
        {
            throw new NotImplementedException();
        }

        public override string[] GetLoadFilamentGCode(ushort position, Nozzle nozzle, double temperature)
        {
            throw new NotImplementedException();
        }

        public override string[] GetLowerPlateGCode()
        {
            throw new NotImplementedException();
        }

        public override string[] GetParkToolGCode()
        {
            throw new NotImplementedException();
        }

        public override string[] GetProbePlateGCode()
        {
            throw new NotImplementedException();
        }

        public override string[] GetProbeToolGCode(ushort position, Nozzle nozzle, double temperature)
        {
            throw new NotImplementedException();
        }

        public override string[] GetPurgeToolGCode(ushort position, Nozzle nozzle, double temperature)
        {
            throw new NotImplementedException();
        }

        public override string[] GetRaisePlateGCode()
        {
            throw new NotImplementedException();
        }

        public override string[] GetRelativeEMovementGCode(double distance, double feedrate)
        {
            throw new NotImplementedException();
        }

        public override string[] GetRelativeXMovementGCode(double distance, double feedrate)
        {
            throw new NotImplementedException();
        }

        public override string[] GetRelativeYMovementGCode(double distance, double feedrate)
        {
            throw new NotImplementedException();
        }

        public override string[] GetRelativeZMovementGCode(double distance, double feedrate)
        {
            throw new NotImplementedException();
        }

        public override string[] GetSelectToolGCode(ushort position)
        {
            throw new NotImplementedException();
        }

        public override string[] GetSetToolTemperatureGCode(ushort position, double temperature)
        {
            throw new NotImplementedException();
        }

        public override string[] GetStartPartProgramGCode(string file_name)
        {
            throw new NotImplementedException();
        }

        public override string[] GetUnloadFilamentGCode(ushort position, Nozzle nozzle, double temperature)
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

        public override Task<bool> SelectPartProgramAsync(string filename, bool from_drive, bool wait, CancellationToken ct)
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

        public override string[] GetSetToolOffsetGCode(ushort position, double x, double y, double z)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> CancelPrintAsync(bool hard_cancel)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> PutFileAsync(string folder, string filename, CancellationToken ct, Optional<IEnumerable<string>> source = default, Optional<IEnumerable<string>> end = default, Optional<uint> source_blocks = default, Action<double> report_progress = null)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> RenameFileAsync(string folder, string old_filename, string new_filename, bool wait, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
    internal class Dummy_MemoryBuffer : FLUX_MemoryBuffer
    {
        public override Dummy_Connection Connection { get; }
        public Dummy_MemoryBuffer(Dummy_Connection connection)
        {
            Connection = connection;
        }
    }
}