using DynamicData.Kernel;
using Modulo3DStandard;
using OSAI;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class OSAI_MemoryBuffer : FLUX_MemoryBuffer
    {
        public override OSAI_Connection Connection { get; }

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
                    _GD_BUFFER_REQ = new ReadVarDoubleRequest((ushort)OSAI_VARCODE.GD_CODE, Connection.ProcessNumber, GD_BUFFER_RANGE.start_addr, count);
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
                    _GW_BUFFER_REQ = new ReadVarWordRequest((ushort)OSAI_VARCODE.GW_CODE, Connection.ProcessNumber, GW_BUFFER_RANGE.start_addr, count);
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
                    _L_BUFFER_REQ = new ReadVarDoubleRequest((ushort)OSAI_VARCODE.L_CODE, Connection.ProcessNumber, L_BUFFER_RANGE.start_addr, count);
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
                    _MW_BUFFER_REQ = new ReadVarWordRequest((ushort)OSAI_VARCODE.MW_CODE, Connection.ProcessNumber, MW_BUFFER_RANGE.start_addr, count);
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

        private bool _HasFullMemoryRead;
        public bool HasFullMemoryRead
        {
            get => _HasFullMemoryRead;
            set => this.RaiseAndSetIfChanged(ref _HasFullMemoryRead, value);
        }

        public OSAI_MemoryBuffer(OSAI_Connection connection)
        {
            Connection = connection;
            GD_BUFFER_CHANGED = this.WhenAnyValue(plc => plc.GD_BUFFER);
            GW_BUFFER_CHANGED = this.WhenAnyValue(plc => plc.GW_BUFFER);
            MW_BUFFER_CHANGED = this.WhenAnyValue(plc => plc.MW_BUFFER);
            L_BUFFER_CHANGED = this.WhenAnyValue(plc => plc.L_BUFFER);
        }
        private (ushort start_addr, ushort end_addr) GetRange(OSAI_VARCODE varcode)
        {
            var variables = Connection.VariableStore.Variables.Values
                .Select(v => v as IOSAI_VariableBase)
                .Where(v => v?.LogicalAddress.VarCode == varcode);

            var start_addr = variables.SelectMany(var =>
            {
                if (var is IOSAI_Array array)
                    return array.Variables.Items.Select(v => v.LogicalAddress.Index);
                return new[] { var.LogicalAddress.Index };
            }).Min();

            var end_addr = variables.SelectMany(var =>
            {
                if (var is IOSAI_Array array)
                    return array.Variables.Items.Select(v => v.LogicalAddress.Index);
                return new[] { var.LogicalAddress.Index };
            }).Max();

            return (start_addr, end_addr);
        }
        public async Task UpdateBufferAsync()
        {
            if (!Connection.Client.HasValue)
                return;

            var gd_response = await Connection.Client.Value.ReadVarDoubleAsync(GD_BUFFER_REQ);
            if (Connection.ProcessResponse(
                gd_response.retval,
                gd_response.ErrClass,
                gd_response.ErrNum))
                GD_BUFFER = gd_response.Value;

            var gw_response = await Connection.Client.Value.ReadVarWordAsync(GW_BUFFER_REQ);
            if (Connection.ProcessResponse(
                gw_response.retval,
                gw_response.ErrClass,
                gw_response.ErrNum))
                GW_BUFFER = gw_response.Value;

            var l_response = await Connection.Client.Value.ReadVarDoubleAsync(L_BUFFER_REQ);
            if (Connection.ProcessResponse(
                l_response.retval,
                l_response.ErrClass,
                l_response.ErrNum))
                L_BUFFER = l_response.Value;

            var mw_response = await Connection.Client.Value.ReadVarWordAsync(MW_BUFFER_REQ);
            if (Connection.ProcessResponse(
                mw_response.retval,
                mw_response.ErrClass,
                mw_response.ErrNum))
                MW_BUFFER = mw_response.Value;
        }
        public IObservable<Optional<ushort>> ObserveWordVar(IOSAI_Variable variable)
        {
            switch (variable.LogicalAddress.VarCode)
            {
                case OSAI_VARCODE.GW_CODE:
                    return GW_BUFFER_CHANGED
                        .Select(buffer =>
                        {
                            if (!buffer.HasValue)
                                return Optional<ushort>.None;

                            var start_addr = GW_BUFFER_RANGE.start_addr;
                            var end_addr = GW_BUFFER_RANGE.end_addr;
                            var index = variable.LogicalAddress.Index;
                            if (index < start_addr || index > end_addr)
                                return Optional<ushort>.None;

                            var position = index - start_addr;
                            if (buffer.Value.Count < position)
                                return Optional<ushort>.None;

                            return buffer.Value[position];
                        });
                case OSAI_VARCODE.MW_CODE:
                    return MW_BUFFER_CHANGED
                        .Select(buffer =>
                        {
                            if (!buffer.HasValue)
                                return Optional<ushort>.None;

                            var start_addr = MW_BUFFER_RANGE.start_addr;
                            var end_addr = MW_BUFFER_RANGE.end_addr;
                            var index = variable.LogicalAddress.Index;
                            if (index < start_addr || index > end_addr)
                                return Optional<ushort>.None;

                            var position = index - start_addr;
                            if (buffer.Value.Count < position)
                                return Optional<ushort>.None;

                            return buffer.Value[position];
                        });
            }
            return Observable.Return(Optional<ushort>.None);
        }
        public IObservable<Optional<double>> ObserveDWordVar(IOSAI_Variable variable)
        {
            switch (variable.LogicalAddress.VarCode)
            {
                case OSAI_VARCODE.GD_CODE:
                    return GD_BUFFER_CHANGED
                        .Select(buffer =>
                        {
                            if (!buffer.HasValue)
                                return Optional<double>.None;

                            var start_addr = GD_BUFFER_RANGE.start_addr;
                            var end_addr = GD_BUFFER_RANGE.end_addr;
                            var index = variable.LogicalAddress.Index;
                            if (index < start_addr || index > end_addr)
                                return Optional<double>.None;

                            var position = index - start_addr;
                            if (buffer.Value.Count < position)
                                return Optional<double>.None;

                            return buffer.Value[position];
                        });
                case OSAI_VARCODE.L_CODE:
                    return L_BUFFER_CHANGED
                        .Select(buffer =>
                        {
                            if (!buffer.HasValue)
                                return Optional<double>.None;

                            var start_addr = L_BUFFER_RANGE.start_addr;
                            var end_addr = L_BUFFER_RANGE.end_addr;
                            var index = variable.LogicalAddress.Index;
                            if (index < start_addr || index > end_addr)
                                return Optional<double>.None;

                            var position = index - start_addr;
                            if (buffer.Value.Count < position)
                                return Optional<double>.None;

                            return buffer.Value[position];
                        });
            }
            return Observable.Return(Optional<double>.None);
        }
    }
}
