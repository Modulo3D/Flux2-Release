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
        public override ushort ArrayBase => 1;

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

        public override string RootPath => "DEVICE";
        public override string PathSeparator => "\\";
        public override string MacroPath => "MACRO";
        public override string QueuePath => "PROGRAMS\\QUEUE";
        public override string StoragePath => "PROGRAMS\\STORAGE";
        public override string InnerQueuePath => "PROGRAMS\\QUEUE\\INNER";
        
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
        public async Task<bool> WriteVariableAsync(OSAI_NamedAddress address, ushort value)
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
        public async Task<Optional<ushort>> ReadNamedUShortAsync(OSAI_NamedAddress address)
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

                return (ushort)read_named_variable_response.Value[0];
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
            return await WriteVariableAsync("!REQ_HOLD", true);
        }
        public override async Task<bool> SelectPartProgramAsync(string partprogram, bool from_drive, bool wait, CancellationToken ct = default)
        {
            try
            {
                if (from_drive)
                {
                    var select_part_program_request = new SelectPartProgramFromDriveRequest(ProcessNumber, $"{StoragePath}\\{partprogram}");
                    var select_part_program_response = await Client.SelectPartProgramFromDriveAsync(select_part_program_request);

                    if (!ProcessResponse(
                        select_part_program_response.retval,
                        select_part_program_response.ErrClass,
                        select_part_program_response.ErrNum))
                        return false;
                }
                else
                {
                    var select_part_program_request = new SelectPartProgramRequest(ProcessNumber, $"{StoragePath}\\{partprogram}");
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
                var remove_file_request = new LogFSRemoveFileRequest($"{folder}\\", filename);
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
            var files_data = new FLUX_FileList(folder);
            Optional<LogFSFindFirstResponse> find_first_result = default;

            try
            {
                find_first_result = await Client.LogFSFindFirstOrDefaultAsync($"{folder}\\*");
                if (!find_first_result.HasValue)
                    return files_data;

                if (!ProcessResponse(
                    find_first_result.Value.Body.retval,
                    find_first_result.Value.Body.ErrClass,
                    find_first_result.Value.Body.ErrNum))
                    return files_data;

                // empty 
                if (find_first_result.Value.Body.Finder == 0xFFFFFFFF)
                    return files_data;

                if (!string.IsNullOrEmpty(find_first_result.Value.Body.FindData.FileName))
                {
                    var file_attributes = (OSAI_FileAttributes)find_first_result.Value.Body.FindData.FileAttributes;
                    files_data.Files.Add(new FLUX_File()
                    {
                        Name = find_first_result.Value.Body.FindData.FileName,
                        Type = file_attributes.HasFlag(OSAI_FileAttributes.FILE_ATTRIBUTE_DIRECTORY) ? FLUX_FileType.Directory : FLUX_FileType.File,
                    });
                }

                var handle = find_first_result.Value.Body.Finder;

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
                    {
                        var file_attributes = (OSAI_FileAttributes)find_next_result.Value.Body.FindData.FileAttributes;
                        files_data.Files.Add(new FLUX_File() 
                        { 
                            Name = find_next_result.Value.Body.FindData.FileName,
                            Type = file_attributes.HasFlag(OSAI_FileAttributes.FILE_ATTRIBUTE_DIRECTORY) ? FLUX_FileType.Directory : FLUX_FileType.File,
                        });
                    }
                }
                while (find_next_result.ConvertOr(r => r.Body.Found, () => false));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
            finally
            {
                var close_request = new LogFSFindCloseRequest(find_first_result.Value.Body.Finder);
                var close_response = await Client.LogFSFindCloseAsync(close_request);

                ProcessResponse(
                    close_response.retval,
                    close_response.ErrClass,
                    close_response.ErrNum);
            }
            
            return files_data;
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
            ushort transaction = 0;

            try
            {
                var remove_file_response = await DeleteFileAsync(folder, "file_upload.tmp", false);
                if (!remove_file_response)
                    return false;

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


                // Write actual gcode
                var chunk_size = 10000;
                if (source.HasValue)
                {
                    foreach (var chunk in get_full_source().AsChunks(chunk_size))
                    {
                        using (var gcode_writer = new StringWriter())
                        {
                            foreach (var line in chunk)
                            {
                                gcode_writer.WriteLine($"N{block_number} {line}");
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

                IEnumerable<string> get_full_source()
                {
                    long current_block = 0;
                    if (source.HasValue)
                    {
                        if (source_blocks.HasValue && source_blocks.Value > 0)
                        {
                            foreach (var line in source.Value)
                            {
                                var progress = (double)current_block++ / source_blocks.Value * 100;
                                if (progress - (int)progress < 0.001)
                                    report_progress((int)progress);
                                yield return line;
                            }
                        }
                        else
                        {
                            foreach (var line in source.Value)
                                yield return line;
                        }
                    }

                    if (end.HasValue)
                    {
                        yield return "; end";
                        foreach (var line in end.Value)
                            yield return line;
                        yield return "";
                    }
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

                var remove_file_response = await DeleteFileAsync(folder, filename, false);
                if (!remove_file_response)
                    return false;

                var rename_response = await RenameFileAsync(folder, "file_upload.tmp", filename, false);;
                if (!rename_response)
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
            if (recovery.ToolNumber < 0)
                yield break;

            yield return $"#!REQ_HOLD = 0.0";
            yield return $"#!IS_HOLD = 0.0";
            yield return $"G500 T{recovery.ToolNumber + 1}";
            yield return $"M4999 [{{FILAMENT_ENDSTOP_1_RESET}}, {recovery.ToolNumber + 1}, 0, 1]";

            for (ushort position = 0; position < recovery.Temperatures.Count; position++)
            {
                var current_tool_temp = recovery.Temperatures
                    .Lookup(position)
                    .ValueOr(() => 0);

                if (current_tool_temp > 50)
                {
                    var hold_temp = $"{current_tool_temp:0}".Replace(",", ".");
                    yield return $"M4104 [{position + 1}, {hold_temp}, 0]";
                }
            }

            var recovery_tool_temp = recovery.Temperatures
                .Lookup((ushort)recovery.ToolNumber)
                .ValueOr(() => 0);

            if (recovery_tool_temp > 50)
            {
                var hold_temp_t = $"{recovery_tool_temp:0}".Replace(",", ".");
                yield return $"M4104 [{recovery.ToolNumber + 1}, {hold_temp_t}, 1]";
            }

            var x_pos = $"{recovery.Positions.Lookup((ushort)0).ValueOr(() => 0):0.000}".Replace(",", ".");
            var y_pos = $"{recovery.Positions.Lookup((ushort)1).ValueOr(() => 0):0.000}".Replace(",", ".");
            var z_pos = $"{recovery.Positions.Lookup((ushort)2).ValueOr(() => 0):0.000}".Replace(",", ".");
            var e_pos = $"{recovery.Positions.Lookup((ushort)3).ValueOr(() => 0):0.000}".Replace(",", ".");

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
        public async Task<Optional<OSAI_AxisPositionDictionary>> GetAxisPosition()
        {
            try
            {
                var axes_ref_request = new GetAxesPositionRequest(ProcessNumber, 0, (ushort)OSAI_AxisPositionSelect.Absolute, AxisNum);
                var axes_ref_response = await Client.GetAxesPositionAsync(axes_ref_request);

                if (!ProcessResponse(
                    axes_ref_response.retval,
                    axes_ref_response.ErrClass,
                    axes_ref_response.ErrNum))
                    return default;

                return new OSAI_AxisPositionDictionary(axes_ref_response.IntPos);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        public override Task<bool> ClearFolderAsync(string folder, bool wait, CancellationToken ct = default) => DeleteFileAsync(folder, "*", wait, ct);

        public override Optional<IEnumerable<string>> GetHomingGCode(params char[] axis)
        {
            return new[] { "(CLS, MACRO\\home_printer)" };
        }
        public override Optional<IEnumerable<string>> GetParkToolGCode()
        {
            return new[] { "(CLS, MACRO\\change_tool, 0)" };
        }
        public override Optional<IEnumerable<string>> GetProbePlateGCode()
        {
            return new[] { "(CLS, MACRO\\probe_plate)" };
        }
        public override Optional<IEnumerable<string>> GetLowerPlateGCode()
        {
            return new[] { "(CLS, MACRO\\lower_plate)" };
        }
        public override Optional<IEnumerable<string>> GetRaisePlateGCode()
        {
            return new[] { "(CLS, MACRO\\raise_plate)" };
        }
        public override Optional<IEnumerable<string>> GetSelectToolGCode(ushort position)
        {
            return new[] { $"(CLS, MACRO\\change_tool, {position + 1})" };
        }
        public override Optional<IEnumerable<string>> GetStartPartProgramGCode(string folder, string file_name)
        {
            return new[] { $"(CLS, {folder}\\{file_name})" };
        }
        public override Optional<IEnumerable<string>> GetSetToolTemperatureGCode(ushort position, double temperature)
        {
            return new[] { $"M4104 [{position + 1}, {temperature}, 0]" };
        }
        public override Optional<IEnumerable<string>> GetProbeToolGCode(ushort position, double temperature)
        {
            return new[] { $"(CLS, MACRO\\probe_tool, {position + 1}, {temperature})" };
        }
        public override Optional<IEnumerable<string>> GetRelativeXMovementGCode(double distance, double feedrate)
        {
            return new[] { $"G1 X>>{distance / 2} Y>>{distance / 2} F{feedrate}".Replace(",", ".") };
        }
        public override Optional<IEnumerable<string>> GetRelativeYMovementGCode(double distance, double feedrate)
        {
            return new[] { $"G1 X>>{distance / 2} Y>>{distance / 2} F{feedrate}".Replace(",", ".") };
        }
        public override Optional<IEnumerable<string>> GetRelativeZMovementGCode(double distance, double feedrate)
        {
            return new[] { $"G1 Z>>{distance} F{feedrate}".Replace(",", ".") };
        }
        public override Optional<IEnumerable<string>> GetRelativeEMovementGCode(double distance, double feedrate)
        {
            return new[] { $"G1 A>>{distance} F{feedrate}".Replace(",", ".") };
        }

        public override async Task<Optional<string>> DownloadFileAsync(string folder, string filename, CancellationToken ct)
        {
            var get_file_request = new GetFileRequest($"{folder}\\{filename}", 1024);
            var get_file_response = await Client.GetFileAsync(get_file_request);

            if (!ProcessResponse(
                get_file_response.retval,
                get_file_response.ErrClass,
                get_file_response.ErrNum))
                return default;

            return get_file_response.Data;
        }

        public override async Task<bool> CreateFolderAsync(string folder, string name, CancellationToken ct)
        {
            var create_dir_request = new LogFSCreateDirRequest($"{folder}\\{name}");
            var create_dir_response = await Client.LogFSCreateDirAsync(create_dir_request);

            if (!ProcessResponse(
                create_dir_response.retval,
                create_dir_response.ErrClass,
                create_dir_response.ErrNum))
                return false;

            return true;
        }

        public override Optional<IEnumerable<string>> GetSetToolOffsetGCode(ushort position, double x, double y, double z)
        {
            var x_offset = (x + y) / 2;
            var y_offset = (x - y) / 2;
            return new[] { $"(UTO, 0, X({x_offset}), Y({y_offset}), Z({z}))" };
        }

        public override async Task<bool> CancelPrintAsync(bool hard_cancel)
        {
            using var put_cancel_print_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var wait_cancel_print_cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return await ExecuteParamacroAsync(new[] { "(CLS, MACRO\\cancel_print)" }, 
                put_cancel_print_cts.Token, true, wait_cancel_print_cts.Token);
        }

        public override async Task<bool> RenameFileAsync(string folder, string old_filename, string new_filename, bool wait, CancellationToken ct = default)
        {
            var rename_request = new LogFSRenameRequest($"{folder}\\{old_filename}", $"{folder}\\{new_filename}");
            var rename_response = await Client.LogFSRenameAsync(rename_request);

            if (!ProcessResponse(
                rename_response.retval,
                rename_response.ErrClass,
                rename_response.ErrNum))
                return false;

            return true;
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
            return new[] { $"M4104 [{position + 1}, 0, 0]" };
        }

        public override Optional<IEnumerable<string>> GetCancelUnloadFilamentGCode(ushort position)
        {
            return new[] { $"M4104 [{position + 1}, 0, 0]" };
        }

        public override Optional<IEnumerable<string>> GetCenterPositionGCode()
        {
            return new[] { "(CLS, MACRO\\center_position)" };
        }

        public override async Task<bool> ExecuteParamacroAsync(IEnumerable<string> paramacro, CancellationToken put_ct, bool wait = false, CancellationToken wait_ct = default, bool can_cancel = false)
        {
            try
            {
                // deselect part program
                using var deselect_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var deselect_result = await DeselectPartProgramAsync(false, true, deselect_ctk.Token);
                if (deselect_result == false)
                    return false;

                using var delete_paramacro_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var delete_paramacro_response = await DeleteFileAsync(StoragePath, "paramacro.mcode", true, delete_paramacro_ctk.Token);

                // put file
                var put_paramacro_response = await PutFileAsync(
                    StoragePath,
                    "paramacro.mcode",
                    put_ct, get_paramacro_gcode().ToOptional());

                if (put_paramacro_response == false)
                    return false;

                // select part program
                using var select_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var select_part_program_response = await SelectPartProgramAsync("paramacro.mcode", true, true, select_ctk.Token);
                if (select_part_program_response == false)
                    return false;

                // Set PLC to Cycle
                return await CycleAsync(true, wait, wait_ct);

                IEnumerable<string> get_paramacro_gcode()
                {
                    foreach (var line in paramacro)
                        yield return line.TrimEnd();
                }
            }
            catch
            {
                return false;
            }
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
            return new[] { $"(CLS, {folder}\\{filename})" };
        }
    }
}
