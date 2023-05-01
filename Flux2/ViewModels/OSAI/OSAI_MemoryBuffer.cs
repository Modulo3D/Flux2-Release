using DynamicData;
using DynamicData.Kernel;
using Modulo3DNet;
using OSAI;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Flux.ViewModels
{

    public class OSAI_MemoryReader : FLUX_MemoryReader<OSAI_MemoryReader, OSAI_MemoryBuffer, Unit>
    {
        public IEnumerable<IOSAI_AsyncVariable> Variables { get; }
        public OSAI_MemoryReader(OSAI_MemoryBuffer memory_buffer, string resource, IEnumerable<IOSAI_AsyncVariable> variables) : base(memory_buffer, resource)
        {
            Variables = variables;
        }
        public override async Task<Optional<Unit>> TryScheduleAsync()
        {
            var has_memory_read = true;
            foreach (var variable in Variables)
            {
                var memory_updated = await variable.UpdateAsync();
                has_memory_read = has_memory_read && memory_updated;
            }
            HasMemoryRead = has_memory_read;
            return default;
        }
    }

    public class OSAI_MemoryReaderGroup : FLUX_MemoryReaderGroup<OSAI_MemoryReaderGroup, OSAI_MemoryBuffer, OSAI_ConnectionProvider, OSAI_VariableStore>
    {
        public OSAI_MemoryReaderGroup(OSAI_MemoryBuffer memory_buffer, TimeSpan period) : base(memory_buffer, period)
        {
        }
        public void AddMemoryReader(IGrouping<OSAI_ReadPriority, IOSAI_AsyncVariable> variables)
        {
            MemoryReaders.AddOrUpdate(new OSAI_MemoryReader(MemoryBuffer, $"{variables.Key}", variables));
        }
    }

    public class OSAI_MemoryBuffer : FLUX_MemoryBuffer<OSAI_MemoryBuffer, OSAI_ConnectionProvider, OSAI_VariableStore>
    {
        public override OSAI_ConnectionProvider ConnectionProvider { get; }

        private (ushort start_addr, ushort end_addr) _GD_BUFFER_RANGE;
        public (ushort start_addr, ushort end_addr) GD_BUFFER_RANGE
        {
            get
            {
                if (_GD_BUFFER_RANGE == default)
                    _GD_BUFFER_RANGE = GetRange(OSAI_VARCODE.GD_CODE);
                return _GD_BUFFER_RANGE;
            }
        }
        public IObservable<Optional<doublearray>> GD_BUFFER_CHANGED { get; }
        private ReadVarDoubleRequest _GD_BUFFER_REQ;
        public ReadVarDoubleRequest GD_BUFFER_REQ
        {
            get
            {
                if (_GD_BUFFER_REQ == default)
                {
                    var count = (ushort)(GD_BUFFER_RANGE.end_addr - GD_BUFFER_RANGE.start_addr + 1);
                    _GD_BUFFER_REQ = new ReadVarDoubleRequest((ushort)OSAI_VARCODE.GD_CODE, OSAI_Connection.ProcessNumber, GD_BUFFER_RANGE.start_addr, count);
                }
                return _GD_BUFFER_REQ;
            }
        }
        private Optional<doublearray> _GD_BUFFER;
        public Optional<doublearray> GD_BUFFER
        {
            get => _GD_BUFFER;
            set => this.RaiseAndSetIfChanged(ref _GD_BUFFER, value);
        }

        private (ushort start_addr, ushort end_addr) _GW_BUFFER_RANGE;
        public (ushort start_addr, ushort end_addr) GW_BUFFER_RANGE
        {
            get
            {
                if (_GW_BUFFER_RANGE == default)
                    _GW_BUFFER_RANGE = GetRange(OSAI_VARCODE.GW_CODE);
                return _GW_BUFFER_RANGE;
            }
        }
        public IObservable<Optional<unsignedshortarray>> GW_BUFFER_CHANGED { get; }
        private ReadVarWordRequest _GW_BUFFER_REQ;
        public ReadVarWordRequest GW_BUFFER_REQ
        {
            get
            {
                if (_GW_BUFFER_REQ == default)
                {
                    var count = (ushort)(GW_BUFFER_RANGE.end_addr - GW_BUFFER_RANGE.start_addr + 1);
                    _GW_BUFFER_REQ = new ReadVarWordRequest((ushort)OSAI_VARCODE.GW_CODE, OSAI_Connection.ProcessNumber, GW_BUFFER_RANGE.start_addr, count);
                }
                return _GW_BUFFER_REQ;
            }
        }
        private Optional<unsignedshortarray> _GW_BUFFER;
        public Optional<unsignedshortarray> GW_BUFFER
        {
            get => _GW_BUFFER;
            set => this.RaiseAndSetIfChanged(ref _GW_BUFFER, value);
        }

        private (ushort start_addr, ushort end_addr) _L_BUFFER_RANGE;
        public (ushort start_addr, ushort end_addr) L_BUFFER_RANGE
        {
            get
            {
                if (_L_BUFFER_RANGE == default)
                    _L_BUFFER_RANGE = GetRange(OSAI_VARCODE.L_CODE);
                return _L_BUFFER_RANGE;
            }
        }
        public IObservable<Optional<doublearray>> L_BUFFER_CHANGED { get; }
        private ReadVarDoubleRequest _L_BUFFER_REQ;
        public ReadVarDoubleRequest L_BUFFER_REQ
        {
            get
            {
                if (_L_BUFFER_REQ == default)
                {
                    var count = (ushort)(L_BUFFER_RANGE.end_addr - L_BUFFER_RANGE.start_addr + 1);
                    _L_BUFFER_REQ = new ReadVarDoubleRequest((ushort)OSAI_VARCODE.L_CODE, OSAI_Connection.ProcessNumber, L_BUFFER_RANGE.start_addr, count);
                }
                return _L_BUFFER_REQ;
            }
        }
        private Optional<doublearray> _L_BUFFER;
        public Optional<doublearray> L_BUFFER
        {
            get => _L_BUFFER;
            set => this.RaiseAndSetIfChanged(ref _L_BUFFER, value);
        }

        private (ushort start_addr, ushort end_addr) _MW_BUFFER_RANGE;
        public (ushort start_addr, ushort end_addr) MW_BUFFER_RANGE
        {
            get
            {
                if (_MW_BUFFER_RANGE == default)
                    _MW_BUFFER_RANGE = GetRange(OSAI_VARCODE.MW_CODE);
                return _MW_BUFFER_RANGE;
            }
        }
        public IObservable<Optional<unsignedshortarray>> MW_BUFFER_CHANGED { get; }
        private ReadVarWordRequest _MW_BUFFER_REQ;
        public ReadVarWordRequest MW_BUFFER_REQ
        {
            get
            {
                if (_MW_BUFFER_REQ == default)
                {
                    var count = (ushort)(MW_BUFFER_RANGE.end_addr - MW_BUFFER_RANGE.start_addr + 1);
                    _MW_BUFFER_REQ = new ReadVarWordRequest((ushort)OSAI_VARCODE.MW_CODE, OSAI_Connection.ProcessNumber, MW_BUFFER_RANGE.start_addr, count);
                }
                return _MW_BUFFER_REQ;
            }
        }
        private Optional<unsignedshortarray> _MW_BUFFER;
        public Optional<unsignedshortarray> MW_BUFFER
        {
            get => _MW_BUFFER;
            set => this.RaiseAndSetIfChanged(ref _MW_BUFFER, value);
        }

        private ObservableAsPropertyHelper<bool> _HasFullMemoryRead;
        public override bool HasFullMemoryRead => _HasFullMemoryRead.Value;

        private SourceCache<OSAI_MemoryReaderGroup, TimeSpan> MemoryReaders { get; set; }

        public OSAI_MemoryBuffer(OSAI_ConnectionProvider connection_provider)
        {
            ConnectionProvider = connection_provider;
            GD_BUFFER_CHANGED = this.WhenAnyValue(plc => plc.GD_BUFFER);
            GW_BUFFER_CHANGED = this.WhenAnyValue(plc => plc.GW_BUFFER);
            MW_BUFFER_CHANGED = this.WhenAnyValue(plc => plc.MW_BUFFER);
            L_BUFFER_CHANGED = this.WhenAnyValue(plc => plc.L_BUFFER);

            SourceCacheRC.Create(this, v => v.MemoryReaders, g => g.Period);
        }
        public override void Initialize(OSAI_VariableStore variableStore)
        {
            var variables = variableStore.Variables;
            var plc_variables = variables.Values
                .SelectMany(var =>
                {
                    return var switch
                    {
                        IFLUX_Array array => array.Variables.Items,
                        IFLUX_Variable variable => new[] { variable },
                        _ => Array.Empty<IFLUX_Variable>(),
                    };
                })
                .Where(v => v is IOSAI_AsyncVariable)
                .Select(v => v as IOSAI_AsyncVariable)
                .GroupBy(v => v.Priority)
                .ToDictionary(group => group.Key, group => group);

            DisposableThread.Start(UpdateBuffersAsync, TimeSpan.FromMilliseconds(25));

            AddModelReader(plc_variables, OSAI_ReadPriority.LOW, TimeSpan.FromMilliseconds(25));
            AddModelReader(plc_variables, OSAI_ReadPriority.HIGH, TimeSpan.FromMilliseconds(50));
            AddModelReader(plc_variables, OSAI_ReadPriority.MEDIUM, TimeSpan.FromMilliseconds(100));
            AddModelReader(plc_variables, OSAI_ReadPriority.ULTRALOW, TimeSpan.FromMilliseconds(200));
            AddModelReader(plc_variables, OSAI_ReadPriority.ULTRAHIGH, TimeSpan.FromMilliseconds(400));

            var has_full_variables_read = MemoryReaders.Connect()
                .TrueForAll(f => f.WhenAnyValue(f => f.HasMemoryRead), r => r);

            _HasFullMemoryRead = Observable.CombineLatest(
                GD_BUFFER_CHANGED,
                GW_BUFFER_CHANGED,
                MW_BUFFER_CHANGED,
                L_BUFFER_CHANGED,
                has_full_variables_read,
                (gd, gw, mw, l, v) => gd.HasValue && gw.HasValue && mw.HasValue && l.HasValue && v)
                .ToProperty(this, v => v.HasFullMemoryRead)
                .DisposeWith(Disposables);
        }
        private void AddModelReader(Dictionary<OSAI_ReadPriority, IGrouping<OSAI_ReadPriority, IOSAI_AsyncVariable>> variables, OSAI_ReadPriority priority, TimeSpan period)
        {
            var priority_variables = variables.Lookup(priority);
            if (!priority_variables.HasValue || !priority_variables.Value.Any())
                return;

            var memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                MemoryReaders.AddOrUpdate(new OSAI_MemoryReaderGroup(this, period));
            memory_reader = MemoryReaders.Lookup(period);
            if (!memory_reader.HasValue)
                return;

            memory_reader.Value.AddMemoryReader(priority_variables.Value);
        }
        private (ushort start_addr, ushort end_addr) GetRange(OSAI_VARCODE varcode)
        {
            var variables = ConnectionProvider.VariableStore.Variables;
            var addr_range = variables.Values
                .SelectMany(var =>
                {
                    return var switch
                    {
                        IFLUX_Array array => array.Variables.Items,
                        IFLUX_Variable variable => new[] { variable },
                        _ => Array.Empty<IFLUX_Variable>(),
                    };
                })
                .Where(v => v is IOSAI_ObservableVariable)
                .Select(v => v as IOSAI_ObservableVariable)
                .Where(v => v.Address.VarCode == varcode)
                .Select(v => v.Address.Index);

            var start_addr = addr_range.Min();
            var end_addr = addr_range.Max();
            return (start_addr, end_addr);
        }
        private async Task UpdateBuffersAsync()
        {
            GD_BUFFER = await UpdateDoubleBufferAsync(GD_BUFFER_REQ);
            L_BUFFER = await UpdateDoubleBufferAsync(L_BUFFER_REQ);

            GW_BUFFER = await UpdateWordBufferAsync(GW_BUFFER_REQ);
            MW_BUFFER = await UpdateWordBufferAsync(MW_BUFFER_REQ);
        }
        private async Task<Optional<doublearray>> UpdateDoubleBufferAsync(ReadVarDoubleRequest request)
        {
            try
            {
                var client = ConnectionProvider.Connection.Client;
                if (!client.HasValue)
                    return default;

                var response = await client.Value.ReadVarDoubleAsync(request);
                if (OSAI_Connection.ProcessResponse(
                    response.retval,
                    response.ErrClass,
                    response.ErrNum))
                    return response.Value;
                return default;
            }
            catch
            {
                return default;
            }
        }
        private async Task<Optional<unsignedshortarray>> UpdateWordBufferAsync(ReadVarWordRequest request)
        {
            try
            {
                var client = ConnectionProvider.Connection.Client;
                if (!client.HasValue)
                    return default;

                var response = await client.Value.ReadVarWordAsync(request);
                if (OSAI_Connection.ProcessResponse(
                    response.retval,
                    response.ErrClass,
                    response.ErrNum))
                    return response.Value;
                return default;
            }
            catch
            {
                return default;
            }
        }
        public static async Task UpdateVariablesAsync(IEnumerable<IOSAI_AsyncVariable> variables)
        {
            foreach (var variable in variables)
                await variable.UpdateAsync();
        }
        public IObservable<Optional<ushort>> ObserveWordVar(IOSAI_Address address)
        {
            return address.VarCode switch
            {
                OSAI_VARCODE.GW_CODE => GW_BUFFER_CHANGED
                    .Select(buffer =>
                    {
                        if (!buffer.HasValue)
                            return Optional<ushort>.None;

                        var start_addr = GW_BUFFER_RANGE.start_addr;
                        var end_addr = GW_BUFFER_RANGE.end_addr;
                        var index = address.Index;
                        if (index < start_addr || index > end_addr)
                            return Optional<ushort>.None;

                        var position = index - start_addr;
                        if (buffer.Value.Count < position)
                            return Optional<ushort>.None;

                        return buffer.Value[position];
                    }),
                OSAI_VARCODE.MW_CODE => MW_BUFFER_CHANGED
                    .Select(buffer =>
                    {
                        if (!buffer.HasValue)
                            return Optional<ushort>.None;

                        var start_addr = MW_BUFFER_RANGE.start_addr;
                        var end_addr = MW_BUFFER_RANGE.end_addr;
                        var index = address.Index;
                        if (index < start_addr || index > end_addr)
                            return Optional<ushort>.None;

                        var position = index - start_addr;
                        if (buffer.Value.Count < position)
                            return Optional<ushort>.None;

                        return buffer.Value[position];
                    }),
                _ => Observable.Return(Optional<ushort>.None),
            };
        }
        public IObservable<Optional<double>> ObserveDWordVar(IOSAI_Address address)
        {
            return address.VarCode switch
            {
                OSAI_VARCODE.GD_CODE => GD_BUFFER_CHANGED
                    .Select(buffer =>
                    {
                        if (!buffer.HasValue)
                            return Optional<double>.None;

                        var start_addr = GD_BUFFER_RANGE.start_addr;
                        var end_addr = GD_BUFFER_RANGE.end_addr;
                        var index = address.Index;
                        if (index < start_addr || index > end_addr)
                            return Optional<double>.None;

                        var position = index - start_addr;
                        if (buffer.Value.Count < position)
                            return Optional<double>.None;

                        return buffer.Value[position];
                    }),
                OSAI_VARCODE.L_CODE => L_BUFFER_CHANGED
                    .Select(buffer =>
                    {
                        if (!buffer.HasValue)
                            return Optional<double>.None;

                        var start_addr = L_BUFFER_RANGE.start_addr;
                        var end_addr = L_BUFFER_RANGE.end_addr;
                        var index = address.Index;
                        if (index < start_addr || index > end_addr)
                            return Optional<double>.None;

                        var position = index - start_addr;
                        if (buffer.Value.Count < position)
                            return Optional<double>.None;

                        return buffer.Value[position];
                    }),
                _ => Observable.Return(Optional<double>.None),
            };
        }
    }
}
