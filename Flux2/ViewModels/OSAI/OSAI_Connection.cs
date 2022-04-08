using DynamicData.Kernel;
using Modulo3DStandard;
using OSAI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class OSAI_Connection : FLUX_Connection<OSAI_VariableStore, OPENcontrolPortTypeClient, OSAI_MemoryBuffer>
    {
        public const ushort AxisNum = 4;
        public ushort ProcessNumber => 1;

        public FluxViewModel Flux { get; }
        private OSAI_MemoryBuffer _MemoryBuffer;
        public override OSAI_MemoryBuffer MemoryBuffer
        {
            get
            {
                if (_MemoryBuffer == default)
                    _MemoryBuffer = new OSAI_MemoryBuffer(this);
                return _MemoryBuffer;
            }
        }

        public override string InnerQueuePath => "PROGRAMS\\QUEUE\\INNER";
        public override string QueuePath => "PROGRAMS\\QUEUE";
        public override string StoragePath => "PROGRAMS\\STORAGE";

        // MEMORY VARIABLES
        public OSAI_Connection(FluxViewModel flux, OSAI_VariableStore variable_store, string address) : base(variable_store, new OPENcontrolPortTypeClient(OPENcontrolPortTypeClient.EndpointConfiguration.OPENcontrol, address))
        {
            Flux = flux;
        }

        // MEMORY R/W
        public async Task<bool> WriteVariableAsync(OSAI_BitIndexAddress address, bool value)
        {
            try
            {
                var write_request = new WriteVarWordBitRequest((ushort)address.VarCode, ProcessNumber, address.Index, address.BitIndex, value ? (ushort)1 : (ushort)0);
                var write_response = await Client.WriteVarWordBitAsync(write_request);

                if (!ProcessResponse(
                    write_response.retval,
                    write_response.ErrClass,
                    write_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public async Task<bool> WriteVariableAsync(OSAI_IndexAddress address, short value)
        {
            try
            {
                var write_request = new WriteVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1, new unsignedshortarray { ShortConverter.Convert(value) });
                var write_response = await Client.WriteVarWordAsync(write_request);

                if (!ProcessResponse(
                    write_response.retval,
                    write_response.ErrClass,
                    write_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public async Task<bool> WriteVariableAsync(OSAI_IndexAddress address, string value)
        {
            try
            {
                if (value.Length > 128)
                    return false;

                var write_request = new WriteVarTextRequest((ushort)address.VarCode, ProcessNumber, address.Index, (ushort)value.Length, value);
                var write_response = await Client.WriteVarTextAsync(write_request);

                if (!ProcessResponse(
                    write_response.retval,
                    write_response.ErrClass,
                    write_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public async Task<bool> WriteVariableAsync(OSAI_IndexAddress address, ushort value)
        {
            try
            {
                var write_request = new WriteVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1, new unsignedshortarray { value });
                var write_response = await Client.WriteVarWordAsync(write_request);

                if (!ProcessResponse(
                    write_response.retval,
                    write_response.ErrClass,
                    write_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public async Task<bool> WriteVariableAsync(OSAI_IndexAddress address, double value)
        {
            try
            {
                var write_request = new WriteVarDoubleRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1, new doublearray { value });
                var write_response = await Client.WriteVarDoubleAsync(write_request);

                if (!ProcessResponse(
                    write_response.retval,
                    write_response.ErrClass,
                    write_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public async Task<bool> WriteVariableAsync(OSAI_IndexAddress address, ushort bit_index, bool value)
        {
            try
            {
                var write_request = new WriteVarWordBitRequest((ushort)address.VarCode, ProcessNumber, address.Index, bit_index, value ? (ushort)1 : (ushort)0);
                var write_response = await Client.WriteVarWordBitAsync(write_request);

                if (!ProcessResponse(
                    write_response.retval,
                    write_response.ErrClass,
                    write_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }

        public async Task<bool> WriteVariableAsync(OSAI_NamedAddress address, bool value)
        {
            try
            {
                var write_named_variable_request = new WriteNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1, new doublearray() { value ? 1.0 : 0.0 });
                var write_named_variable_response = await Client.WriteNamedVarDoubleAsync(write_named_variable_request);

                if (!ProcessResponse(
                    write_named_variable_response.retval,
                    write_named_variable_response.ErrClass,
                    write_named_variable_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public async Task<bool> WriteVariableAsync(OSAI_NamedAddress address, short value)
        {
            try
            {
                var write_named_variable_request = new WriteNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1, new doublearray() { value });
                var write_named_variable_response = await Client.WriteNamedVarDoubleAsync(write_named_variable_request);

                if (!ProcessResponse(
                    write_named_variable_response.retval,
                    write_named_variable_response.ErrClass,
                    write_named_variable_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public async Task<bool> WriteVariableAsync(OSAI_NamedAddress address, double value)
        {
            try
            {
                var write_named_variable_request = new WriteNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1, new doublearray() { value });
                var write_named_variable_response = await Client.WriteNamedVarDoubleAsync(write_named_variable_request);

                if (!ProcessResponse(
                    write_named_variable_response.retval,
                    write_named_variable_response.ErrClass,
                    write_named_variable_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public async Task<bool> WriteVariableAsync(OSAI_NamedAddress address, string value, ushort lenght)
        {
            try
            {
                if (value.Length > lenght)
                    return false;

                var bytes_array = Encoding.ASCII.GetBytes(value);
                var write_named_variable_request = new WriteNamedVarByteArrayRequest(ProcessNumber, address.Name, (ushort)bytes_array.Length, 0, -1, -1, bytes_array);
                var write_named_variable_response = await Client.WriteNamedVarByteArrayAsync(write_named_variable_request);

                if (!ProcessResponse(
                    write_named_variable_response.retval,
                    write_named_variable_response.ErrClass,
                    write_named_variable_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }

        public async Task<Optional<bool>> ReadBoolAsync(OSAI_BitIndexAddress address)
        {
            try
            {
                var read_request = new ReadVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.ReadVarWordAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return read_response.Value[0].IsBitSet(address.BitIndex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        public async Task<Optional<ushort>> ReadUshortAsync(OSAI_IndexAddress address)
        {
            try
            {
                var read_request = new ReadVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.ReadVarWordAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return read_response.Value[0];
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        public async Task<Optional<short>> ReadShortAsync(OSAI_IndexAddress address)
        {
            try
            {
                var read_request = new ReadVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.ReadVarWordAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return ShortConverter.Convert(read_response.Value[0]);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        public async Task<Optional<string>> ReadTextAsync(OSAI_IndexAddress address)
        {
            try
            {
                var read_request = new ReadVarTextRequest((ushort)address.VarCode, ProcessNumber, address.Index, 128);
                var read_response = await Client.ReadVarTextAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return read_response.Text;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        public async Task<Optional<double>> ReadDoubleAsync(OSAI_IndexAddress address)
        {
            try
            {
                var read_request = new ReadVarDoubleRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.ReadVarDoubleAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return read_response.Value[0];
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        public async Task<Optional<bool>> ReadBoolAsync(OSAI_IndexAddress address, ushort bit_index)
        {
            try
            {
                var read_request = new ReadVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.ReadVarWordAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return read_response.Value[0].IsBitSet(bit_index);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }

        public async Task<Optional<bool>> ReadNamedBoolAsync(OSAI_NamedAddress address)
        {
            try
            {
                var read_named_variable_request = new ReadNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1);
                var read_named_variable_response = await Client.ReadNamedVarDoubleAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return read_named_variable_response.Value[0] == 1.0 ? true : false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        public async Task<Optional<short>> ReadNamedShortAsync(OSAI_NamedAddress address)
        {
            try
            {
                var read_named_variable_request = new ReadNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1);
                var read_named_variable_response = await Client.ReadNamedVarDoubleAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return (short)read_named_variable_response.Value[0];
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        public async Task<Optional<double>> ReadNamedDoubleAsync(OSAI_NamedAddress address)
        {
            try
            {
                var read_named_variable_request = new ReadNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1);
                var read_named_variable_response = await Client.ReadNamedVarDoubleAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return read_named_variable_response.Value[0];
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        public async Task<Optional<string>> ReadNamedStringAsync(OSAI_NamedAddress address, ushort lenght)
        {
            try
            {
                var read_named_variable_request = new ReadNamedVarByteArrayRequest(ProcessNumber, address.Name, lenght, address.Index, -1, -1);
                var read_named_variable_response = await Client.ReadNamedVarByteArrayAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return Encoding.ASCII.GetString(read_named_variable_response.Value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }

        public async Task<(OSAI_CloseResponse response, CommunicationState state)> CloseAsync()
        {
            try
            {
                await Client.CloseAsync();
                return (OSAI_CloseResponse.CLOSE_SUCCESS, CommunicationState.Closed);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return (OSAI_CloseResponse.CLOSE_EXCEPTION, CommunicationState.Faulted);
            }
        }

        // BASIC FUNCTIONS
        public bool ProcessResponse(ushort retval, uint ErrClass, uint ErrNum, string @params = default, [CallerMemberName] string callerMember = null)
        {
            if (retval == 0)
            {
                Debug.WriteLine($"Errore in {callerMember} {@params}, classe: {ErrClass}, Codice {ErrNum}");
                return false;
            }
            return true;
        }

        // WAIT MEMORY
        public async Task<bool> WaitBootPhaseAsync(Func<OSAI_BootPhase, bool> phase_func, TimeSpan dueTime, TimeSpan sample, TimeSpan timeout)
        {
            var read_boot_phase = Observable.Timer(dueTime, sample)
                .SelectMany(t => ReadVariableAsync(c => c.BOOT_PHASE));

            return await WaitUtils.WaitForOptionalAsync(read_boot_phase,
                phase => phase_func(phase), timeout);
        }
        public async Task<bool> WaitProcessModeAsync(Func<OSAI_ProcessMode, bool> status_func, TimeSpan dueTime, TimeSpan sample, TimeSpan timeout)
        {
            var read_boot_mode = Observable.Timer(dueTime, sample)
                .SelectMany(t => ReadVariableAsync(c => c.PROCESS_MODE));

            return await WaitUtils.WaitForOptionalAsync(read_boot_mode,
                process => status_func(process), timeout);
        }

        // BASIC OPERATIONS
        public async Task<bool> ShutdownAsync()
        {
            try
            {
                var shutdown_request = new BootShutDownRequest();
                var shutdown_response = await Client.BootShutDownAsync(shutdown_request);

                if (!ProcessResponse(
                    shutdown_response.retval,
                    shutdown_response.ErrClass,
                    shutdown_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public async Task<bool> HoldAsync(bool on)
        {
            try
            {
                var hold_request = new HoldRequest(ProcessNumber, on ? (ushort)0 : (ushort)1);
                var hold_response = await Client.HoldAsync(hold_request);

                if (!ProcessResponse(
                    hold_response.retval,
                    hold_response.ErrClass,
                    hold_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public override async Task<bool> ResetAsync()
        {
            try
            {
                var reset_request = new ResetRequest(ProcessNumber);
                var reset_response = await Client.ResetAsync(reset_request);

                if (!ProcessResponse(
                    reset_response.retval,
                    reset_response.ErrClass,
                    reset_response.ErrNum))
                    return false;

                return await WaitProcessStatusAsync(
                    status => status == FLUX_ProcessStatus.IDLE,
                    TimeSpan.FromSeconds(0.1),
                    TimeSpan.FromSeconds(0.1),
                    TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public override async Task<bool> CycleAsync(bool start, bool wait, CancellationToken ct = default)
        {
            try
            {
                var cycle_request = new CycleRequest(ProcessNumber, start ? (ushort)1 : (ushort)0);
                var cycle_response = await Client.CycleAsync(cycle_request);

                if (!ProcessResponse(
                    cycle_response.retval,
                    cycle_response.ErrClass,
                    cycle_response.ErrNum))
                    return false;

                if (wait && ct != CancellationToken.None)
                {
                    if (!await WaitProcessStatusAsync(
                           status => status == FLUX_ProcessStatus.CYCLE,
                           TimeSpan.FromSeconds(0),
                           TimeSpan.FromSeconds(0.1),
                           TimeSpan.FromSeconds(5)))
                        return false;

                    if (!await WaitProcessStatusAsync(
                        status => status == FLUX_ProcessStatus.IDLE,
                        TimeSpan.FromSeconds(0),
                        TimeSpan.FromSeconds(0.1),
                        ct))
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }

        public override async Task<bool> DeselectPartProgramAsync(bool from_drive, bool wait, CancellationToken ct = default)
        {
            try
            {
                if (from_drive)
                {
                    var select_part_program_request = new SelectPartProgramFromDriveRequest(ProcessNumber, "");
                    var select_part_program_response = await Client.SelectPartProgramFromDriveAsync(select_part_program_request);

                    if (!ProcessResponse(
                        select_part_program_response.retval,
                        select_part_program_response.ErrClass,
                        select_part_program_response.ErrNum))
                        return false;
                }
                else
                {
                    var select_part_program_request = new SelectPartProgramRequest(ProcessNumber, "");
                    var select_part_program_response = await Client.SelectPartProgramAsync(select_part_program_request);

                    if (!ProcessResponse(
                        select_part_program_response.retval,
                        select_part_program_response.ErrClass,
                        select_part_program_response.ErrNum))
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public override async Task<bool> HoldAsync()
        {
            return await WriteVariableAsync(VariableStore.REQ_HOLD, true);
        }
        public override async Task<bool> SelectPartProgramAsync(string filename, bool from_drive, bool wait, CancellationToken ct = default)
        {
            try
            {
                if (from_drive)
                {
                    var select_part_program_request = new SelectPartProgramFromDriveRequest(ProcessNumber, $"{StoragePath}\\{filename}");
                    var select_part_program_response = await Client.SelectPartProgramFromDriveAsync(select_part_program_request);

                    if (!ProcessResponse(
                        select_part_program_response.retval,
                        select_part_program_response.ErrClass,
                        select_part_program_response.ErrNum))
                        return false;
                }
                else
                {
                    var select_part_program_request = new SelectPartProgramRequest(ProcessNumber, $"{StoragePath}\\{filename}");
                    var select_part_program_response = await Client.SelectPartProgramAsync(select_part_program_request);

                    if (!ProcessResponse(
                        select_part_program_response.retval,
                        select_part_program_response.ErrClass,
                        select_part_program_response.ErrNum))
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }

        // FILES
        public override async Task<bool> DeleteFileAsync(string folder, string filename, bool wait, CancellationToken ct = default)
        {
            try
            {
                var remove_file_request = new LogFSRemoveFileRequest(folder, filename);
                var remove_file_response = await Client.LogFSRemoveFileAsync(remove_file_request);

                if (remove_file_response.ErrClass == 5 &&
                    remove_file_response.ErrNum == 17)
                    return true;

                if (!ProcessResponse(
                    remove_file_response.retval,
                    remove_file_response.ErrClass,
                    remove_file_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public override async Task<Optional<FLUX_FileList>> ListFilesAsync(string folder, CancellationToken ct = default)
        {
            try
            {
                var find_first_result = await Client.LogFSFindFirstOrDefaultAsync($"{folder}\\*");

                if (!ProcessResponse(
                    find_first_result.Body.retval,
                    find_first_result.Body.ErrClass,
                    find_first_result.Body.ErrNum))
                    return default;

                var files_data = new FLUX_FileList(folder);

                // empty 
                if (find_first_result.Body.Finder == 0xFFFFFFFF)
                    return files_data;

                if (!string.IsNullOrEmpty(find_first_result.Body.FindData.FileName))
                    files_data.Files.Add(new FLUX_File() { Type = FLUX_FileType.File, Name = find_first_result.Body.FindData.FileName });

                var handle = find_first_result.Body.Finder;

                Optional<LogFSFindNextResponse> find_next_result;
                do
                {
                    find_next_result = await Client.LogFSFindNextAsync(handle);
                    if (!find_next_result.HasValue)
                        return files_data;

                    if (!ProcessResponse(
                        find_next_result.Value.Body.retval,
                        find_next_result.Value.Body.ErrClass,
                        find_next_result.Value.Body.ErrNum))
                        return files_data;

                    if (!string.IsNullOrEmpty(find_next_result.Value.Body.FindData.FileName))
                        files_data.Files.Add(new FLUX_File() { Type = FLUX_FileType.File, Name = find_next_result.Value.Body.FindData.FileName });
                }
                while (find_next_result.ConvertOr(r => r.Body.Found, () => false));

                var close_request = new LogFSFindCloseRequest(find_first_result.Body.Finder);
                var close_response = await Client.LogFSFindCloseAsync(close_request);

                if (!ProcessResponse(
                    find_next_result.Value.Body.retval,
                    find_next_result.Value.Body.ErrClass,
                    find_next_result.Value.Body.ErrNum))
                    return files_data;

                return files_data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        public override async Task<bool> PutFileAsync(
            string folder,
            string filename,
            CancellationToken ct,
            Optional<IEnumerable<string>> source,
            Optional<IEnumerable<string>> end = default,
            Optional<uint> source_blocks = default,
            Action<double> report_progress = default)
        {
            ushort file_id;
            uint block_number = 0;
            uint skipped_blocks = 0;
            ushort transaction = 0;

            try
            {
                var remove_file_request = new LogFSRemoveFileRequest(folder, "file_upload.tmp");
                var remove_file_response = await Client.LogFSRemoveFileAsync(remove_file_request);

                var create_file_request = new LogFSCreateFileRequest($"{folder}\\file_upload.tmp");
                var create_file_response = await Client.LogFSCreateFileAsync(create_file_request);

                if (!ProcessResponse(
                    create_file_response.retval,
                    create_file_response.ErrClass,
                    create_file_response.ErrNum))
                    return false;

                var open_file_request = new LogFSOpenFileRequest($"{folder}\\file_upload.tmp", true, 0, 0);
                var open_file_response = await Client.LogFSOpenFileAsync(open_file_request);

                if (!ProcessResponse(
                    open_file_response.retval,
                    open_file_response.ErrClass,
                    open_file_response.ErrNum))
                    return false;
                file_id = open_file_response.FileID;


                // Write recovery
                var recovery = await ReadVariableAsync(c => c.MCODE_RECOVERY);
                if (recovery.HasValue)
                {
                    skipped_blocks = ((OSAI_MCodeRecovery)recovery.Value).BlockNumber;
                    using (var recovery_writer = new StringWriter())
                    {
                        recovery_writer.WriteLine("; recovery");
                        foreach (var line in GenerateRecoveryLines((OSAI_MCodeRecovery)recovery.Value))
                            recovery_writer.WriteLine(line);
                        recovery_writer.WriteLine("");

                        var byte_data = Encoding.UTF8.GetBytes(recovery_writer.ToString());
                        var write_record_request = new LogFSWriteRecordRequest(file_id, transaction++, (uint)byte_data.Length, byte_data);
                        var write_record_response = await Client.LogFSWriteRecordAsync(write_record_request);

                        if (!ProcessResponse(
                            write_record_response.retval,
                            write_record_response.ErrClass,
                            write_record_response.ErrNum))
                        {
                            var close_file_request = new LogFSCloseFileRequest(file_id, (ushort)(transaction - 1));
                            var close_file_response = await Client.LogFSCloseFileAsync(close_file_request);

                            ProcessResponse(
                                close_file_response.retval,
                                close_file_response.ErrClass,
                                close_file_response.ErrNum);

                            return false;
                        }
                    }
                }

                // Write actual gcode
                var chunk_counter = 0;
                var chunk_size = 10000;
                var actual_blocks_count = source_blocks.Convert(s => s - skipped_blocks);
                if (source.HasValue)
                {
                    foreach (var chunk in source.Value.UIntSkip(skipped_blocks).AsChunks(chunk_size))
                    {
                        using (var gcode_writer = new StringWriter())
                        {
                            foreach (var line in chunk)
                            {
                                gcode_writer.WriteLine($"N{skipped_blocks + block_number} {line}");
                                block_number++;
                            }
                            gcode_writer.WriteLine("");

                            var byte_data = Encoding.UTF8.GetBytes(gcode_writer.ToString());
                            var write_record_request = new LogFSWriteRecordRequest(file_id, transaction++, (uint)byte_data.Length, byte_data);
                            var write_record_response = await Client.LogFSWriteRecordAsync(write_record_request);

                            if (!ProcessResponse(
                                write_record_response.retval,
                                write_record_response.ErrClass,
                                write_record_response.ErrNum))
                            {
                                var close_file_request = new LogFSCloseFileRequest(file_id, (ushort)(transaction - 1));
                                var close_file_response = await Client.LogFSCloseFileAsync(close_file_request);

                                ProcessResponse(
                                    close_file_response.retval,
                                    close_file_response.ErrClass,
                                    close_file_response.ErrNum);

                                return false;
                            }
                        }
                    }

                    chunk_counter += chunk_size;
                    if (actual_blocks_count.HasValue)
                    {
                        var percentage = (double)chunk_counter / actual_blocks_count.Value * 100;
                        report_progress?.Invoke(Math.Max(0, Math.Min(100, percentage)));
                    }
                }

                var byte_data_newline = Encoding.UTF8.GetBytes(Environment.NewLine);
                var write_record_request_newline = new LogFSWriteRecordRequest(file_id, transaction++, (uint)byte_data_newline.Length, byte_data_newline);
                var write_record_response_newline = await Client.LogFSWriteRecordAsync(write_record_request_newline);

                if (!ProcessResponse(
                    write_record_response_newline.retval,
                    write_record_response_newline.ErrClass,
                    write_record_response_newline.ErrNum))
                {
                    var close_file_request = new LogFSCloseFileRequest(file_id, (ushort)(transaction - 1));
                    var close_file_response = await Client.LogFSCloseFileAsync(close_file_request);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }

            try
            {
                if (file_id == 0)
                    return false;

                var close_file_request = new LogFSCloseFileRequest(file_id, transaction++);
                var close_file_response = await Client.LogFSCloseFileAsync(close_file_request);
                if (!ProcessResponse(
                    close_file_response.retval,
                    close_file_response.ErrClass,
                    close_file_response.ErrNum))
                    return false;

                var remove_file_request = new LogFSRemoveFileRequest(folder, filename);
                var remove_file_response = await Client.LogFSRemoveFileAsync(remove_file_request);

                var rename_request = new LogFSRenameRequest($"{folder}\\file_upload.tmp", $"{folder}\\{filename}");
                var rename_responde = await Client.LogFSRenameAsync(rename_request);
                if (!ProcessResponse(
                    rename_responde.retval,
                    rename_responde.ErrClass,
                    rename_responde.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }

        public IEnumerable<string> GenerateRecoveryLines(OSAI_MCodeRecovery recovery)
        {
            yield return $"#!REQ_HOLD = 0.0";
            yield return $"#!IS_HOLD = 0.0";
            yield return $"G500 T{recovery.ToolNumber + 1}";
            yield return $"M4999 [{{WIRE_ENDSTOP_1_RESET}}, {recovery.ToolNumber + 1}, 0, 1]";

            for (int tool = 0; tool < recovery.Temperatures.Count; tool++)
            {
                var current_tool_temp = recovery.Temperatures
                    .Lookup($"{tool}")
                    .ValueOr(() => 0);

                if (current_tool_temp > 50)
                {
                    var hold_temp = $"{current_tool_temp:0}".Replace(",", ".");
                    yield return $"M4104 [{tool + 1}, {hold_temp}, 0]";
                }
            }

            var recovery_tool_temp = recovery.Temperatures
                .Lookup($"{recovery.ToolNumber}")
                .ValueOr(() => 0);

            if (recovery_tool_temp > 50)
            {
                var hold_temp_t = $"{recovery_tool_temp:0}".Replace(",", ".");
                yield return $"M4104 [{recovery.ToolNumber + 1}, {hold_temp_t}, 1]";
            }

            var x_pos = $"{recovery.Positions.Lookup("X").ValueOr(() => 0):0.000}".Replace(",", ".");
            var y_pos = $"{recovery.Positions.Lookup("Y").ValueOr(() => 0):0.000}".Replace(",", ".");
            var z_pos = $"{recovery.Positions.Lookup("Z").ValueOr(() => 0):0.000}".Replace(",", ".");
            var e_pos = $"{recovery.Positions.Lookup("E").ValueOr(() => 0):0.000}".Replace(",", ".");

            yield return $"G1 X{x_pos} Y{y_pos} F15000";
            yield return $"G1 Z{z_pos} F5000";

            yield return $"G92 A0";
            yield return $"G1 A1 F2000";

            yield return $"G92 A{e_pos}";
        }

        // AXIS MANAGEMENT
        public async Task<bool> AxesRefAsync(params char[] axes)
        {
            try
            {
                var axes_ref_request = new AxesRefRequest(ProcessNumber, (ushort)axes.Length, string.Join("", axes));
                var axes_ref_response = await Client.AxesRefAsync(axes_ref_request);

                if (!ProcessResponse(
                    axes_ref_response.retval,
                    axes_ref_response.ErrClass,
                    axes_ref_response.ErrNum))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }
        public override Task<bool> ClearFolderAsync(string folder, bool wait, CancellationToken ct = default) => DeleteFileAsync(folder, "*", wait, ct);

        public override Optional<IEnumerable<string>> GetHomingGCode(params char[] axis)
        {
            return new[]
            {
                "G400",
                "(UAO, 0)",
                "G201 X(@PRBP_XPOS) Y(@PRBP_YPOS) Z400 F10000"
            };
        }
        public override Optional<IEnumerable<string>> GetParkToolGCode()
        {
            return new[] { $"G500 T0" };
        }
        public override Optional<IEnumerable<string>> GetProbePlateGCode()
        {
            return new[] { "G401" };
        }
        public override Optional<IEnumerable<string>> GetLowerPlateGCode()
        {
            return new[] { "G0 Z400" };
        }
        public override Optional<IEnumerable<string>> GetRaisePlateGCode()
        {
            throw new NotImplementedException();
        }
        public override Optional<IEnumerable<string>> GetGotoReaderGCode(ushort position)
        {
            return new[] { $"G501 T{position + 1}" };
        }
        public override Optional<IEnumerable<string>> GetSelectToolGCode(ushort position)
        {
            return new[] { $"G500 T{position + 1}" };
        }
        public override Optional<IEnumerable<string>> GetGotoPurgePositionGCode(ushort position)
        {
            return new[]
            {
                $"G500 T{position + 1}" ,
                "(UAO, 0)",
                "G201 X(@PURGE_XPOS) Y(@PURGE_YPOS) F10000"
            };
        }
        public override Optional<IEnumerable<string>> GetStartPartProgramGCode(string file_name)
        {
            throw new NotImplementedException();
        }
        public override Optional<IEnumerable<string>> GetSetToolTemperatureGCode(ushort position, double temperature)
        {
            throw new NotImplementedException();
        }
        public override Optional<IEnumerable<string>> GetProbeToolGCode(ushort position, Nozzle nozzle, double temperature)
        {
            return new[] { $"G402 T{position + 1} S{temperature}" };
        }
        public override Optional<IEnumerable<string>> GetPurgeToolGCode(ushort position, Nozzle nozzle, double temperature)
        {
            return new[] { $"G507 T{position + 1} S{temperature}" };
        }
        public override Optional<IEnumerable<string>> GetLoadFilamentGCode(ushort position, Nozzle nozzle, double temperature)
        {
            return new[] { $"G505 T{position + 1} S{temperature}" };
        }
        public override Optional<IEnumerable<string>> GetUnloadFilamentGCode(ushort position, Nozzle nozzle, double temperature)
        {
            return new[] { $"G506 T{position + 1} S{temperature}" };
        }
        public override Optional<IEnumerable<string>> GetRelativeXMovementGCode(double distance, double feedrate) => new[] { $"G1 X>>{distance / 2} Y>>{distance / 2} F{feedrate}".Replace(",", ".") };
        public override Optional<IEnumerable<string>> GetRelativeYMovementGCode(double distance, double feedrate) => new[] { $"G1 X>>{distance / 2} Y>>{distance / 2} F{feedrate}".Replace(",", ".") };
        public override Optional<IEnumerable<string>> GetRelativeZMovementGCode(double distance, double feedrate) => new[] { $"G1 Z>>{distance} F{feedrate}".Replace(",", ".") };
        public override Optional<IEnumerable<string>> GetRelativeEMovementGCode(double distance, double feedrate) => new[] { $"G1 A>>{distance} F{feedrate}".Replace(",", ".") };

        public override Task<Optional<string>> DownloadFileAsync(string folder, string filename, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> CreateFolderAsync(string folder, string name, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override Optional<IEnumerable<string>> GetSetToolOffsetGCode(ushort position, double x, double y, double z)
        {
            throw new NotImplementedException();
        }

        public override async Task<bool> CancelPrintAsync(bool hard_cancel)
        {
            using var put_cancel_print_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var wait_cancel_print_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return await ExecuteParamacroAsync(new[] { "G508" }, put_cancel_print_cts.Token, true, wait_cancel_print_cts.Token);
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
    }
}
