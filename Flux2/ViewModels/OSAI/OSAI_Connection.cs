using DynamicData.Kernel;
using Modulo3DNet;
using OSAI;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Flux.ViewModels
{
    public class OSAI_Connection : FLUX_Connection<OSAI_ConnectionProvider, OSAI_VariableStore, OPENcontrolPortTypeClient>
    {
        public const ushort AxisNum = 4;
        public const ushort ProcessNumber = 1;

        public override string RootPath => "DEVICE";
        public override string MacroPath => "MACRO";
        public override string QueuePath => "PROGRAMS\\QUEUE";
        public override string EventPath => "PROGRAMS\\EVENTS";
        public override string StoragePath => "PROGRAMS\\STORAGE";
        public override string JobEventPath => CombinePaths(EventPath, "JOB");
        public override string InnerQueuePath => CombinePaths(QueuePath, "INNER");
        public override string ExtrusionEventPath => CombinePaths(EventPath, "EXTR");
        public override string CombinePaths(params string[] paths) => string.Join("\\", paths);

        public FluxViewModel Flux { get; }

        // MEMORY VARIABLES
        public OSAI_Connection(FluxViewModel flux, OSAI_ConnectionProvider connection_provider) : base(connection_provider)
        {
            Flux = flux;
        }

        // CONNECT
        public override async Task<bool> CloseAsync()
        {
            try
            {
                if (!Client.HasValue)
                    return true;
                await Client.Value.CloseAsync();
                Client = null;
                return true;
            }
            catch
            {
                return false;
            }
        }
        public override async Task<bool> ConnectAsync()
        {
            try
            {
                if (!await CloseAsync())
                    return false;

                if (!Flux.NetProvider.PLCNetworkConnectivity)
                    return false;

                var core_settings = Flux.SettingsProvider.CoreSettings.Local;
                var plc_address = core_settings.PLCAddress;
                if (!plc_address.HasValue)
                    return false;

                Client = new OPENcontrolPortTypeClient(OPENcontrolPortTypeClient.EndpointConfiguration.OPENcontrol, plc_address.Value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // MEMORY R/W
        public async Task<bool> WriteVariableAsync(OSAI_BitIndexAddress address, bool value)
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                var write_request = new WriteVarWordBitRequest((ushort)address.VarCode, ProcessNumber, address.Index, address.BitIndex, value ? (ushort)1 : (ushort)0);
                var write_response = await Client.Value.WriteVarWordBitAsync(write_request);

                if (!ProcessResponse(
                    write_response.retval,
                    write_response.ErrClass,
                    write_response.ErrNum))
                    return false;

                return true;
            }
            catch { return false; }
        }
        public async Task<bool> WriteVariableAsync(OSAI_IndexAddress address, short value)
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                var write_request = new WriteVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1, new unsignedshortarray { ShortConverter.Convert(value) });
                var write_response = await Client.Value.WriteVarWordAsync(write_request);

                if (!ProcessResponse(
                    write_response.retval,
                    write_response.ErrClass,
                    write_response.ErrNum))
                    return false;

                return true;
            }
            catch { return false; }
        }
        public async Task<bool> WriteVariableAsync(OSAI_IndexAddress address, string value)
        {
            try
            {
                if (value.Length > 128)
                    return false;

                if (!Client.HasValue)
                    return false;

                var write_request = new WriteVarTextRequest((ushort)address.VarCode, ProcessNumber, address.Index, (ushort)value.Length, value);
                var write_response = await Client.Value.WriteVarTextAsync(write_request);

                if (!ProcessResponse(
                    write_response.retval,
                    write_response.ErrClass,
                    write_response.ErrNum))
                    return false;

                return true;
            }
            catch { return false; }
        }
        public async Task<bool> WriteVariableAsync(OSAI_IndexAddress address, ushort value)
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                var write_request = new WriteVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1, new unsignedshortarray { value });
                var write_response = await Client.Value.WriteVarWordAsync(write_request);

                if (!ProcessResponse(
                    write_response.retval,
                    write_response.ErrClass,
                    write_response.ErrNum))
                    return false;

                return true;
            }
            catch { return false; }
        }
        public async Task<bool> WriteVariableAsync(OSAI_IndexAddress address, double value)
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                var write_request = new WriteVarDoubleRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1, new doublearray { value });
                var write_response = await Client.Value.WriteVarDoubleAsync(write_request);

                if (!ProcessResponse(
                    write_response.retval,
                    write_response.ErrClass,
                    write_response.ErrNum))
                    return false;

                return true;
            }
            catch { return false; }
        }
        public async Task<bool> WriteVariableAsync(OSAI_IndexAddress address, ushort bit_index, bool value)
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                var write_request = new WriteVarWordBitRequest((ushort)address.VarCode, ProcessNumber, address.Index, bit_index, value ? (ushort)1 : (ushort)0);
                var write_response = await Client.Value.WriteVarWordBitAsync(write_request);

                if (!ProcessResponse(
                    write_response.retval,
                    write_response.ErrClass,
                    write_response.ErrNum))
                    return false;

                return true;
            }
            catch { return false; }
        }

        public async Task<bool> WriteVariableAsync(OSAI_NamedAddress address, bool value)
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                var write_named_variable_request = new WriteNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1, new doublearray() { value ? 1.0 : 0.0 });
                var write_named_variable_response = await Client.Value.WriteNamedVarDoubleAsync(write_named_variable_request);

                if (!ProcessResponse(
                    write_named_variable_response.retval,
                    write_named_variable_response.ErrClass,
                    write_named_variable_response.ErrNum))
                    return false;

                return true;
            }
            catch { return false; }
        }
        public async Task<bool> WriteVariableAsync(OSAI_NamedAddress address, short value)
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                var write_named_variable_request = new WriteNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1, new doublearray() { value });
                var write_named_variable_response = await Client.Value.WriteNamedVarDoubleAsync(write_named_variable_request);

                if (!ProcessResponse(
                    write_named_variable_response.retval,
                    write_named_variable_response.ErrClass,
                    write_named_variable_response.ErrNum))
                    return false;

                return true;
            }
            catch { return false; }
        }
        public async Task<bool> WriteVariableAsync(OSAI_NamedAddress address, ushort value)
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                var write_named_variable_request = new WriteNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1, new doublearray() { value });
                var write_named_variable_response = await Client.Value.WriteNamedVarDoubleAsync(write_named_variable_request);

                if (!ProcessResponse(
                    write_named_variable_response.retval,
                    write_named_variable_response.ErrClass,
                    write_named_variable_response.ErrNum))
                    return false;

                return true;
            }
            catch { return false; }
        }
        public async Task<bool> WriteVariableAsync(OSAI_NamedAddress address, double value)
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                var write_named_variable_request = new WriteNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1, new doublearray() { value });
                var write_named_variable_response = await Client.Value.WriteNamedVarDoubleAsync(write_named_variable_request);

                if (!ProcessResponse(
                    write_named_variable_response.retval,
                    write_named_variable_response.ErrClass,
                    write_named_variable_response.ErrNum))
                    return false;

                return true;
            }
            catch { return false; }
        }
        public async Task<bool> WriteVariableAsync(OSAI_NamedAddress address, string value, ushort lenght)
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                if (value.Length > lenght)
                    return false;

                var bytes_array = Encoding.ASCII.GetBytes(value);
                var write_named_variable_request = new WriteNamedVarByteArrayRequest(ProcessNumber, address.Name, (ushort)bytes_array.Length, 0, -1, -1, bytes_array);
                var write_named_variable_response = await Client.Value.WriteNamedVarByteArrayAsync(write_named_variable_request);

                if (!ProcessResponse(
                    write_named_variable_response.retval,
                    write_named_variable_response.ErrClass,
                    write_named_variable_response.ErrNum))
                    return false;

                return true;
            }
            catch { return false; }
        }

        public async Task<ValueResult<bool>> ReadBoolAsync(OSAI_BitIndexAddress address)
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                var read_request = new ReadVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.Value.ReadVarWordAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return read_response.Value[0].IsBitSet(address.BitIndex);
            }
            catch { return default; }
        }
        public async Task<ValueResult<ushort>> ReadUShortAsync(OSAI_IndexAddress address)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_request = new ReadVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.Value.ReadVarWordAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return read_response.Value[0];
            }
            catch { return default; }
        }
        public async Task<ValueResult<short>> ReadShortAsync(OSAI_IndexAddress address)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_request = new ReadVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.Value.ReadVarWordAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return ShortConverter.Convert(read_response.Value[0]);
            }
            catch { return default; }
        }
        public async Task<ValueResult<string>> ReadTextAsync(OSAI_IndexAddress address)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_request = new ReadVarTextRequest((ushort)address.VarCode, ProcessNumber, address.Index, 128);
                var read_response = await Client.Value.ReadVarTextAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return read_response.Text;
            }
            catch { return default; }
        }
        public async Task<ValueResult<double>> ReadDoubleAsync(OSAI_IndexAddress address)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_request = new ReadVarDoubleRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.Value.ReadVarDoubleAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return read_response.Value[0];
            }
            catch { return default; }
        }
        public async Task<ValueResult<bool>> ReadBoolAsync(OSAI_IndexAddress address, ushort bit_index)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_request = new ReadVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.Value.ReadVarWordAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return read_response.Value[0].IsBitSet(bit_index);
            }
            catch { return default; }
        }

        public async Task<ValueResult<bool>> ReadNamedBoolAsync(OSAI_NamedAddress address)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_named_variable_request = new ReadNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1);
                var read_named_variable_response = await Client.Value.ReadNamedVarDoubleAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return read_named_variable_response.Value[0] == 1.0;
            }
            catch { return default; }
        }
        public async Task<ValueResult<short>> ReadNamedShortAsync(OSAI_NamedAddress address)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_named_variable_request = new ReadNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1);
                var read_named_variable_response = await Client.Value.ReadNamedVarDoubleAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return (short)read_named_variable_response.Value[0];
            }
            catch { return default; }
        }
        public async Task<ValueResult<ushort>> ReadNamedUShortAsync(OSAI_NamedAddress address)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_named_variable_request = new ReadNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1);
                var read_named_variable_response = await Client.Value.ReadNamedVarDoubleAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return (ushort)read_named_variable_response.Value[0];
            }
            catch { return default; }
        }
        public async Task<ValueResult<double>> ReadNamedDoubleAsync(OSAI_NamedAddress address)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_named_variable_request = new ReadNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1);
                var read_named_variable_response = await Client.Value.ReadNamedVarDoubleAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return read_named_variable_response.Value[0];
            }
            catch { return default; }
        }
        public async Task<ValueResult<string>> ReadNamedStringAsync(OSAI_NamedAddress address, ushort lenght)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_named_variable_request = new ReadNamedVarByteArrayRequest(ProcessNumber, address.Name, lenght, address.Index, -1, -1);
                var read_named_variable_response = await Client.Value.ReadNamedVarByteArrayAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return Encoding.ASCII.GetString(read_named_variable_response.Value);
            }
            catch { return default; }
        }

        public async Task<ValueResult<TOut>> ReadBoolAsync<TOut>(OSAI_BitIndexAddress address, Func<bool, TOut> convert_func)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_request = new ReadVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.Value.ReadVarWordAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return convert_func(read_response.Value[0].IsBitSet(address.BitIndex));
            }
            catch { return default; }
        }
        public async Task<ValueResult<TOut>> ReadUShortAsync<TOut>(OSAI_IndexAddress address, Func<ushort, TOut> convert_func)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_request = new ReadVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.Value.ReadVarWordAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return convert_func(read_response.Value[0]);
            }
            catch { return default; }
        }
        public async Task<ValueResult<TOut>> ReadShortAsync<TOut>(OSAI_IndexAddress address, Func<short, TOut> convert_func)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_request = new ReadVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.Value.ReadVarWordAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return convert_func(ShortConverter.Convert(read_response.Value[0]));
            }
            catch { return default; }
        }
        public async Task<ValueResult<TOut>> ReadTextAsync<TOut>(OSAI_IndexAddress address, Func<string, TOut> convert_func)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_request = new ReadVarTextRequest((ushort)address.VarCode, ProcessNumber, address.Index, 128);
                var read_response = await Client.Value.ReadVarTextAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return convert_func(read_response.Text);
            }
            catch { return default; }
        }
        public async Task<ValueResult<TOut>> ReadDoubleAsync<TOut>(OSAI_IndexAddress address, Func<double, TOut> convert_func)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_request = new ReadVarDoubleRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.Value.ReadVarDoubleAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return convert_func(read_response.Value[0]);
            }
            catch { return default; }
        }
        public async Task<ValueResult<TOut>> ReadBoolAsync<TOut>(OSAI_IndexAddress address, ushort bit_index, Func<bool, TOut> convert_func)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_request = new ReadVarWordRequest((ushort)address.VarCode, ProcessNumber, address.Index, 1);
                var read_response = await Client.Value.ReadVarWordAsync(read_request);

                if (!ProcessResponse(
                    read_response.retval,
                    read_response.ErrClass,
                    read_response.ErrNum))
                    return default;

                return convert_func(read_response.Value[0].IsBitSet(bit_index));
            }
            catch { return default; }
        }

        public async Task<ValueResult<TOut>> ReadNamedBoolAsync<TOut>(OSAI_NamedAddress address, Func<bool, TOut> convert_func)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_named_variable_request = new ReadNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1);
                var read_named_variable_response = await Client.Value.ReadNamedVarDoubleAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return convert_func(read_named_variable_response.Value[0] == 1.0);
            }
            catch { return default; }
        }
        public async Task<ValueResult<TOut>> ReadNamedShortAsync<TOut>(OSAI_NamedAddress address, Func<short, TOut> convert_func)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_named_variable_request = new ReadNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1);
                var read_named_variable_response = await Client.Value.ReadNamedVarDoubleAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return convert_func((short)read_named_variable_response.Value[0]);
            }
            catch { return default; }
        }
        public async Task<ValueResult<TOut>> ReadNamedUShortAsync<TOut>(OSAI_NamedAddress address, Func<ushort, TOut> convert_func)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_named_variable_request = new ReadNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1);
                var read_named_variable_response = await Client.Value.ReadNamedVarDoubleAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return convert_func((ushort)read_named_variable_response.Value[0]);
            }
            catch { return default; }
        }
        public async Task<ValueResult<TOut>> ReadNamedDoubleAsync<TOut>(OSAI_NamedAddress address, Func<double, TOut> convert_func)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_named_variable_request = new ReadNamedVarDoubleRequest(ProcessNumber, address.Name, 1, address.Index, -1, -1);
                var read_named_variable_response = await Client.Value.ReadNamedVarDoubleAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return convert_func(read_named_variable_response.Value[0]);
            }
            catch { return default; }
        }
        public async Task<ValueResult<TOut>> ReadNamedStringAsync<TOut>(OSAI_NamedAddress address, ushort lenght, Func<string, TOut> convert_func)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var read_named_variable_request = new ReadNamedVarByteArrayRequest(ProcessNumber, address.Name, lenght, address.Index, -1, -1);
                var read_named_variable_response = await Client.Value.ReadNamedVarByteArrayAsync(read_named_variable_request);

                if (!ProcessResponse(
                    read_named_variable_response.retval,
                    read_named_variable_response.ErrClass,
                    read_named_variable_response.ErrNum,
                    address.ToString()))
                    return default;

                return convert_func(Encoding.ASCII.GetString(read_named_variable_response.Value));
            }
            catch { return default; }
        }

        // BASIC FUNCTIONS
        public static bool ProcessResponse(ushort retval, uint ErrClass, uint ErrNum, string @params = default, [CallerMemberName] string callerMember = null)
        {
            if (retval == 0)
            {
                Debug.WriteLine($"Errore in {callerMember} {@params}, classe: {ErrClass}, Codice {ErrNum}");
                return false;
            }
            return true;
        }
        public override InnerQueueGCodes GenerateInnerQueueGCodes(FluxJob job)
        {
            try
            {
                var extruder_count = Flux.SettingsProvider.ExtrudersCount;
                if (!extruder_count.HasValue)
                    return default;

                var start = GCodeString.Create(
                    GetStartPartProgramGCode(StoragePath, $"{job.MCodeKey}", new BlockNumber(0, BlockType.None)));

                var end = log_event(FluxEventType.End);
                var pause = log_event(FluxEventType.Pause);
                var cancel = log_event(FluxEventType.Cancel);
                var resume = log_event(FluxEventType.Resume);
                var end_filament = log_event(FluxEventType.EndFilament);

                var begin = GCodeString.Create(

                    // write job
                    $"LS0 = \"{job.JobKey}\"",
                    "M4001[7, 0, 0, 0]",

                    // write extr key
                    Enumerable.Range(0, extruder_count.Value.machine_extruders * extruder_count.Value.mixing_extruders)
                        .Select(e =>
                        {
                            var extr_key = extrusion_key(e);
                            if (!extr_key.HasValue)
                                return default;
                            return GCodeString.Create(
                                $"LS0 = \"{extr_key.Value}\"",
                                $"M4001[7, 3 + {e}, 0, 0]");
                        })
                        .ToArray(),

                    log_event(FluxEventType.Begin));

                return new InnerQueueGCodes()
                {
                    End = end,
                    Start = start,
                    Begin = begin,
                    Pause = pause,
                    Cancel = cancel,
                    Resume = resume,
                    EndFilament = end_filament,
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return default;
            }

            Optional<ExtrusionKey> extrusion_key(int position)
            {
                return Flux.Feeders.Feeders.Lookup((ushort)position)
                    .Convert(f => f.SelectedMaterial)
                    .Convert(m => m.ExtrusionKey);
            }

            GCodeString log_event(FluxEventType event_type)
            {
                if (job.QueuePosition < 0)
                    return default;

                return GCodeString.Create(

                    // get event path
                    $"LS0 = \"{CombinePaths(JobEventPath, $"{job.MCodeKey};{job.JobKey}")}\"",

                    // get current date
                    $"(GDT, D2, T0, SC0.10, SC11.11)", 
                    "SC10.1 = \";\"",

                    // append file
                    $"(OPN, 1, ?LS0, A, A)",
                    $"(WRT, 1, \"{event_type.ToEnumString()};\", SC0.22)",
                    "(CLO, 1)");
            }
        }

        public override Task<bool> InitializeVariablesAsync(CancellationToken ct)
        {
            return Task.FromResult(true);
        }
        // WAIT MEMORY
        public async Task<bool> WaitBootPhaseAsync(Func<OSAI_BootPhase, bool> phase_func, TimeSpan dueTime, TimeSpan sample, TimeSpan throttle, TimeSpan timeout)
        {
            var read_boot_phase = Observable.Timer(dueTime, sample)
                .SelectMany(t => ReadVariableAsync(c => c.BOOT_PHASE));

            return await WaitUtils.WaitForValueResultAsync(read_boot_phase,
                throttle, phase => phase_func(phase), timeout);
        }
        public async Task<bool> WaitProcessModeAsync(Func<OSAI_ProcessMode, bool> status_func, TimeSpan dueTime, TimeSpan sample, TimeSpan throttle, TimeSpan timeout)
        {
            var read_boot_mode = Observable.Timer(dueTime, sample)
                .SelectMany(t => ReadVariableAsync(c => c.PROCESS_MODE));

            return await WaitUtils.WaitForValueResultAsync(read_boot_mode,
                throttle, process => status_func(process), timeout);
        }

        // BASIC OPERATIONS
        public async Task<ValueResult<FLUX_AxisPosition>> GetAxesPositionAsync(OSAI_AxisPositionSelect select)
        {
            try
            {
                if (!Client.HasValue)
                    return default;

                var axes_pos_request = new GetAxesPositionRequest(ProcessNumber, 0, (ushort)select, AxisNum);
                var axes_pos_response = await Client.Value.GetAxesPositionAsync(axes_pos_request);

                if (!ProcessResponse(
                    axes_pos_response.retval,
                    axes_pos_response.ErrClass,
                    axes_pos_response.ErrNum))
                    return default;

                return (FLUX_AxisPosition)axes_pos_response.IntPos.ToImmutableDictionary(p => (char)p.AxisName, p => p.position);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return default;
            }
        }
        public async Task<bool> AxesRefAsync(params char[] axes)
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                var axes_ref_request = new AxesRefRequest(ProcessNumber, (ushort)axes.Length, string.Join("", axes));
                var axes_ref_response = await Client.Value.AxesRefAsync(axes_ref_request);

                if (!ProcessResponse(
                    axes_ref_response.retval,
                    axes_ref_response.ErrClass,
                    axes_ref_response.ErrNum))
                    return false;

                return true;
            }
            catch { return false; }
        }

        // CONTROL
        public override async Task<bool> StopAsync()
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                var reset_request = new ResetRequest(ProcessNumber);
                var reset_response = await Client.Value.ResetAsync(reset_request);

                if (!ProcessResponse(
                    reset_response.retval,
                    reset_response.ErrClass,
                    reset_response.ErrNum))
                    return false;

                return true;
            }
            catch { return false; }
        }
        public override async Task<bool> CancelAsync()
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                var reset_request = new ResetRequest(ProcessNumber);
                var reset_response = await Client.Value.ResetAsync(reset_request);

                if (!ProcessResponse(
                    reset_response.retval,
                    reset_response.ErrClass,
                    reset_response.ErrNum))
                    return false;

                var wait_idle = await ConnectionProvider.WaitProcessStatusAsync(
                    s => s == FLUX_ProcessStatus.IDLE,
                    TimeSpan.FromSeconds(0.1),
                    TimeSpan.FromSeconds(0.1),
                    TimeSpan.FromSeconds(0.2),
                    TimeSpan.FromSeconds(5));

                if (!wait_idle)
                    return false;

                using var paramacro_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                return await ExecuteParamacroAsync(new[]
                {
                    GetExecuteMacroGCode(InnerQueuePath, "cancel.g"),
                    GetExecuteMacroGCode(MacroPath, "end_print")
                }, paramacro_cts.Token);
            }
            catch { return false; }
        }
        public override async Task<bool> PauseAsync(bool end_filament)
        {
            return await WriteVariableAsync("!REQ_HOLD", true);
        }
        public override async Task<bool> ExecuteParamacroAsync(GCodeString paramacro, CancellationToken put_ct, bool can_cancel = false)
        {
            try
            {
                if (!paramacro.HasValue)
                    return false;

                var deselect_part_program_request = new SelectPartProgramFromDriveRequest(ProcessNumber, $"");
                var deselect_part_program_response = await Client.Value.SelectPartProgramFromDriveAsync(deselect_part_program_request);

                if (!ProcessResponse(
                    deselect_part_program_response.retval,
                    deselect_part_program_response.ErrClass,
                    deselect_part_program_response.ErrNum))
                    return false;

                using var delete_paramacro_ctk = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var remove_file_response = await DeleteAsync(StoragePath, "paramacro.mcode", delete_paramacro_ctk.Token);
                if (!remove_file_response)
                    return false;

                // put file
                var put_paramacro_response = await PutFileAsync(
                    StoragePath,
                    "paramacro.mcode", true,
                    put_ct, get_paramacro_gcode().ToOptional());

                if (put_paramacro_response == false)
                    return false;

                // Set PLC to Cycle
                var select_part_program_request = new SelectPartProgramFromDriveRequest(ProcessNumber, CombinePaths(StoragePath, "paramacro.mcode"));
                var select_part_program_response = await Client.Value.SelectPartProgramFromDriveAsync(select_part_program_request);

                if (!ProcessResponse(
                    select_part_program_response.retval,
                    select_part_program_response.ErrClass,
                    select_part_program_response.ErrNum))
                    return false;

                var cycle_request = new CycleRequest(ProcessNumber, 1);
                var cycle_response = await Client.Value.CycleAsync(cycle_request);

                if (!ProcessResponse(
                    cycle_response.retval,
                    cycle_response.ErrClass,
                    cycle_response.ErrNum))
                    return false;

                return true;

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
        public override GCodeString GetBeginPartProgramGCode()
        {
            return GCodeString.Create(
                base.GetBeginPartProgramGCode(),
                 "; preprocessing",
                "(GTO, end_preprocess)",

                "M4140[0, 0]",
                "M4141[0, 0]",
                "M4104[0, 0, 0]",
                "M4999[0, 0, 0, 0]",

                "(CLS, MACRO\\probe_plate)",
                "(CLS, MACRO\\end_print)",
                "(CLS, MACRO\\home_printer)",
                "(CLS, MACRO\\change_tool, 0)",

                "G92 A0",
                "G1 X0 Y0 Z0 F1000",

                "\"end_preprocess\"",
                "(PAS)");
        }
        // FILES
        public override async Task<bool> PutFileAsync(
            string folder,
            string filename,
            bool is_paramacro,
            CancellationToken ct,
            GCodeString source = default,
            GCodeString start = default,
            GCodeString end = default,
            Optional<BlockNumber> source_blocks = default,
            Action<double> report_progress = default)
        {
            ushort file_id;
            uint block_number = 0;
            ushort transaction = 0;

            try
            {
                if (!Client.HasValue)
                    return false;

                // upload a paramacro
                if (is_paramacro)
                {
                    var path_name = CombinePaths(folder, filename);
                    var gcode = GCodeString.Create(start, source, end, Environment.NewLine).ToString();
                    var put_file_request = new PutFileRequest(gcode, (uint)gcode.Length, path_name);
                    var put_file_response = await Client.Value.PutFileAsync(put_file_request);

                    if (!ProcessResponse(
                        put_file_response.retval,
                        put_file_response.ErrClass,
                        put_file_response.ErrNum))
                        return false;

                    return true;
                }

                // upload a partprogram
                using var delete_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var remove_file_response = await DeleteAsync(folder, "file_upload.tmp", delete_cts.Token);
                if (!remove_file_response)
                    return false;

                var create_file_request = new LogFSCreateFileRequest($"{folder}\\file_upload.tmp");
                var create_file_response = await Client.Value.LogFSCreateFileAsync(create_file_request);

                if (!ProcessResponse(
                    create_file_response.retval,
                    create_file_response.ErrClass,
                    create_file_response.ErrNum))
                    return false;

                var open_file_request = new LogFSOpenFileRequest($"{folder}\\file_upload.tmp", true, 0, 0);
                var open_file_response = await Client.Value.LogFSOpenFileAsync(open_file_request);

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
                        using var gcode_writer = new StringWriter();
                        foreach (var line in chunk)
                            gcode_writer.WriteLine(line);
                        gcode_writer.WriteLine("");

                        var byte_data = Encoding.UTF8.GetBytes(gcode_writer.ToString());
                        var write_record_request = new LogFSWriteRecordRequest(file_id, transaction++, (uint)byte_data.Length, byte_data);
                        var write_record_response = await Client.Value.LogFSWriteRecordAsync(write_record_request);

                        if (!ProcessResponse(
                            write_record_response.retval,
                            write_record_response.ErrClass,
                            write_record_response.ErrNum))
                        {
                            var close_file_request = new LogFSCloseFileRequest(file_id, (ushort)(transaction - 1));
                            var close_file_response = await Client.Value.LogFSCloseFileAsync(close_file_request);

                            ProcessResponse(
                                close_file_response.retval,
                                close_file_response.ErrClass,
                                close_file_response.ErrNum);

                            return false;
                        }
                    }
                }

                var byte_data_newline = Encoding.UTF8.GetBytes(Environment.NewLine);
                var write_record_request_newline = new LogFSWriteRecordRequest(file_id, transaction++, (uint)byte_data_newline.Length, byte_data_newline);
                var write_record_response_newline = await Client.Value.LogFSWriteRecordAsync(write_record_request_newline);

                if (!ProcessResponse(
                    write_record_response_newline.retval,
                    write_record_response_newline.ErrClass,
                    write_record_response_newline.ErrNum))
                {
                    var close_file_request = new LogFSCloseFileRequest(file_id, (ushort)(transaction - 1));
                    var close_file_response = await Client.Value.LogFSCloseFileAsync(close_file_request);
                    return false;
                }

                IEnumerable<string> get_full_source()
                {
                    if (start.HasValue)
                    {
                        foreach (var line in start)
                            yield return line;
                    }

                    long current_block = 0;
                    if (source.HasValue)
                    {
                        foreach (var line in source)
                        {
                            yield return $"N{block_number++} {line}";

                            if (!source_blocks.HasValue)
                                continue;

                            if (source_blocks.Value == 0)
                                continue;

                            var progress = (double)current_block++ / source_blocks.Value * 100;
                            if (progress - (int)progress < 0.001)
                                report_progress?.Invoke((int)progress);
                        }
                    }

                    if (end.HasValue)
                    {
                        foreach (var line in end)
                            yield return line;
                    }

                    yield return "";
                }
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                if (file_id == 0)
                    return false;

                var close_file_request = new LogFSCloseFileRequest(file_id, transaction++);
                var close_file_response = await Client.Value.LogFSCloseFileAsync(close_file_request);
                if (!ProcessResponse(
                    close_file_response.retval,
                    close_file_response.ErrClass,
                    close_file_response.ErrNum))
                    return false;

                using var delete_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var remove_file_response = await DeleteAsync(folder, filename, delete_cts.Token);
                if (!remove_file_response)
                    return false;

                using var rename_cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var rename_response = await RenameAsync(folder, "file_upload.tmp", filename, rename_cts.Token); ;
                if (!rename_response)
                    return false;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public override Task<bool> ClearFolderAsync(string folder, CancellationToken ct)
        {
            return DeleteAsync(folder, "*", ct);
        }
        public override Task<Stream> GetFileStreamAsync(string folder, string name, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
        public override async Task<bool> DeleteAsync(string folder, string filename, CancellationToken ct)
        {
            try
            {
                if (!Client.HasValue)
                    return false;

                if (filename != "*")
                {
                    var file_list = await ListFilesAsync(folder, ct);
                    if (!file_list.HasValue)
                        return false;

                    if (!file_list.Value.Files.Any(f => f.Name == filename))
                        return true;
                }

                var remove_file_request = new LogFSRemoveFileRequest($"{folder}\\", filename);
                var remove_file_response = await Client.Value.LogFSRemoveFileAsync(remove_file_request);

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
            catch { return false; }
        }
        public override async Task<bool> CreateFolderAsync(string folder, string name, CancellationToken ct)
        {
            if (!Client.HasValue)
                return false;

            var create_dir_request = new LogFSCreateDirRequest($"{folder}\\{name}");
            var create_dir_response = await Client.Value.LogFSCreateDirAsync(create_dir_request);

            if (!ProcessResponse(
                create_dir_response.retval,
                create_dir_response.ErrClass,
                create_dir_response.ErrNum))
                return false;

            return true;
        }
        public override async Task<Optional<FLUX_FileList>> ListFilesAsync(string folder, CancellationToken ct)
        {
            var files_data = new FLUX_FileList(folder);
            Optional<LogFSFindFirstResponse> find_first_result = default;

            try
            {
                if (!Client.HasValue)
                    return default;

                find_first_result = await Client.Value.LogFSFindFirstOrDefaultAsync($"{folder}\\*");
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

                var file = parse_file(find_first_result.Value.Body.FindData);
                if (file.HasValue)
                    files_data.Files.Add(file.Value);

                var handle = find_first_result.Value.Body.Finder;

                Optional<LogFSFindNextResponse> find_next_result;
                do
                {
                    find_next_result = await Client.Value.LogFSFindNextAsync(handle);
                    if (!find_next_result.HasValue)
                        return files_data;

                    if (!ProcessResponse(
                        find_next_result.Value.Body.retval,
                        find_next_result.Value.Body.ErrClass,
                        find_next_result.Value.Body.ErrNum))
                        return files_data;

                    file = parse_file(find_next_result.Value.Body.FindData);
                    if (file.HasValue)
                        files_data.Files.Add(file.Value);
                }
                while (find_next_result.ConvertOr(r => r.Body.Found, () => false));
            }
            catch { return default; }
            finally
            {
                if (find_first_result.HasValue)
                {
                    var close_request = new LogFSFindCloseRequest(find_first_result.Value.Body.Finder);
                    var close_response = await Client.Value.LogFSFindCloseAsync(close_request);

                    ProcessResponse(
                        close_response.retval,
                        close_response.ErrClass,
                        close_response.ErrNum);
                }
            }

            return files_data;

            static Optional<FLUX_File> parse_file(FILEFINDDATA data)
            {
                if (string.IsNullOrEmpty(data.FileName))
                    return default;

                var file_name = data.FileName;
                var file_attributes = (OSAI_FileAttributes)data.FileAttributes;
                var file_size = (ulong)data.FileSizeHigh << 32 | data.FileSizeLow;
                var has_directory_flag = file_attributes.HasFlag(OSAI_FileAttributes.FILE_ATTRIBUTE_DIRECTORY);
                var file_type = has_directory_flag ? FLUX_FileType.Directory : FLUX_FileType.File;

                var file_date = DateTime.FromFileTime((long)data.HighDateLastWriteTime << 32 | data.LowDateLastWriteTime);

                return new FLUX_File()
                {
                    Name = file_name,
                    Type = file_type,
                    Date = file_date,
                    Size = file_size,
                };
            }
        }
        public override Task<bool> PutFileStreamAsync(string folder, string name, Stream data, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
        public override async Task<Optional<string>> GetFileAsync(string folder, string filename, CancellationToken ct)
        {
            if (!Client.HasValue)
                return default;

            var get_file_request = new GetFileRequest($"{folder}\\{filename}", 1024);
            var get_file_response = await Client.Value.GetFileAsync(get_file_request);

            if (!ProcessResponse(
                get_file_response.retval,
                get_file_response.ErrClass,
                get_file_response.ErrNum))
                return default;

            return get_file_response.Data;
        }
        public override async Task<bool> RenameAsync(string folder, string old_filename, string new_filename, CancellationToken ct)
        {
            if (!Client.HasValue)
                return false;

            var rename_request = new LogFSRenameRequest($"{folder}\\{old_filename}", $"{folder}\\{new_filename}");
            var rename_response = await Client.Value.LogFSRenameAsync(rename_request);

            if (!ProcessResponse(
                rename_response.retval,
                rename_response.ErrClass,
                rename_response.ErrNum))
                return false;

            return true;
        }

        // GCODE
        public override GCodeString GetParkToolGCode()
        {
            return "(CLS, MACRO\\change_tool, 0)";
        }
        public override GCodeString GetProbePlateGCode()
        {
            return "(CLS, MACRO\\probe_plate)";
        }
        public override GCodeString GetLowerPlateGCode()
        {
            return "(CLS, MACRO\\lower_plate)";
        }
        public override GCodeString GetRaisePlateGCode()
        {
            return "(CLS, MACRO\\raise_plate)";
        }
        public override GCodeString GetSetLowCurrentGCode()
        {
            throw new NotImplementedException();
        }
        public override GCodeString GetProbeMagazineGCode()
        {
            throw new NotImplementedException();
        }
        public override GCodeString GetCenterPositionGCode()
        {
            return new[] { "(CLS, MACRO\\center_position)" };
        }
        public override GCodeString GetExitPartProgramGCode()
        {
            return new[] { "(REL)" };
        }
        public override GCodeString GetHomingGCode(params char[] axis)
        {
            return "(CLS, MACRO\\home_printer)";
        }
        public override GCodeString GetManualCalibrationPositionGCode()
        {
            throw new NotImplementedException();
        }
        public override GCodeString GetSelectToolGCode(ArrayIndex position)
        {
            return $"(CLS, MACRO\\change_tool, {position.GetArrayBaseIndex()})";
        }
        public override GCodeString GetMovementGCode(FLUX_AxisMove axis_move, FLUX_AxisTransform transform)
        {
            var inverse_transform_move = transform.InverseTransformMove(axis_move);
            if (!inverse_transform_move.HasValue)
                return default;

            return $"G1 {inverse_transform_move.Value.GetAxisPosition(m => m.Relative ? ">>" : "")} {axis_move.GetFeedrate('F')}";
        }
        public override GCodeString GetCancelLoadFilamentGCode(ArrayIndex position)
        {
            return new[] { $"M4104 [{position.GetArrayBaseIndex()}, 0, 0]" };
        }
        public override GCodeString GetCancelUnloadFilamentGCode(ArrayIndex position)
        {
            return new[] { $"M4104 [{position.GetArrayBaseIndex()}, 0, 0]" };
        }
        public override GCodeString GetResetPositionGCode(FLUX_AxisPosition axis_position, FLUX_AxisTransform transform)
        {
            var inverse_transform_position = transform.InverseTransformPosition(axis_position, false);
            if (!inverse_transform_position.HasValue)
                return default;

            return $"G92 {inverse_transform_position.Value.GetAxisPosition()}";
        }
        public override GCodeString GetExecuteMacroGCode(string folder, string filename)
        {
            return new[] { $"(CLS, {folder}\\{filename})" };
        }
        public override GCodeString GetProbeToolGCode(ArrayIndex position, double temperature)
        {
            return $"(CLS, MACRO\\probe_tool, {position.GetArrayBaseIndex()}, {temperature})";
        }
        public override GCodeString GetFilamentSensorSettingsGCode(ArrayIndex position, bool enabled)
        {
            return $"M4999 [{{FILAMENT_ENDSTOP_1_RESET}}, {position.GetArrayBaseIndex()}, 0, {(enabled ? 1 : 0)}]";
        }
        public override GCodeString GetSetToolOffsetGCode(ArrayIndex position, double x, double y, double z)
        {
            var x_offset = (x + y) / 2;
            var y_offset = (x - y) / 2;
            return new[] { $"(UTO, 0, X({x_offset}), Y({y_offset}), Z({z}))" };
        }
        protected override GCodeString GetSetToolTemperatureGCodeInner(ArrayIndex position, double temperature, bool wait)
        {
            return $"M4104 [{position.GetArrayBaseIndex()}, {temperature}, {(wait ? 1 : 0)}]";
        }
        protected override GCodeString GetSetPlateTemperatureGCodeInner(ArrayIndex position, double temperature, bool wait)
        {
            return $"M4141 [{temperature}, {(wait ? 1 : 0)}]";
        }
        protected override GCodeString GetSetChamberTemperatureGCodeInner(ArrayIndex position, double temperature, bool wait)
        {
            return $"M4140 [{temperature}, {(wait ? 1 : 0)}]";
        }
        public override GCodeString GetStartPartProgramGCode(string folder, string filename, BlockNumber start_block)
        {
            return new[]
            {
                $"LS0 = \"{folder}\"",
                $"LS1 = \"{filename}\"",
                $"M4026[0, 1, {start_block}]"
            };
        }
        public override GCodeString GetSetExtruderMixingGCode(ArrayIndex machine_extruder, ArrayIndex mixing_extruder)
        {
            throw new NotImplementedException();
        }
    }
}
