using DynamicData.Kernel;
using Modulo3DStandard;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class MCodeQueueViewModel : RemoteControl<MCodeQueueViewModel>, IFluxMCodeQueueViewModel
    {
        public MCodesViewModel MCodes { get; }

        private ObservableAsPropertyHelper<Optional<IFluxMCodeStorageViewModel>> _Storage;
        public Optional<IFluxMCodeStorageViewModel> Storage => _Storage.Value;

        public ObservableAsPropertyHelper<Optional<string>> _MCodeName;
        [RemoteOutput(true)]
        public Optional<string> MCodeName => _MCodeName.Value;

        [RemoteOutput(false)]
        public ushort QueueIndex { get; }

        public Guid MCodeGuid { get; }

        private ObservableAsPropertyHelper<bool> _CurrentIndex;
        [RemoteOutput(true)]
        public bool CurrentIndex => _CurrentIndex.Value;

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
        
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MoveUpCommand { get; }
        
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> MoveDownCommand { get; }

        public MCodeQueueViewModel(MCodesViewModel mcodes, ushort queue_index, Guid queue_guid) : base($"mcodeQueue??{queue_index}")
        {
            MCodes = mcodes;
            MCodeGuid = queue_guid;
            QueueIndex = queue_index;

            _CurrentIndex = MCodes.Flux.ConnectionProvider
                .ObserveVariable(c => c.QUEUE_POS)
                .Select(q => q.HasValue && q.Value == queue_index)
                .ToProperty(this, v => v.CurrentIndex)
                .DisposeWith(Disposables);

            _Storage = MCodes.AvaiableMCodes
                .Connect()
                .WatchOptional(queue_guid)
                .ToProperty(this, v => v.Storage)
                .DisposeWith(Disposables);

            _MCodeName = this.WhenAnyValue(v => v.Storage)
                .Convert(s => s.Analyzer.MCode.Name)
                .ToProperty(this, v => v.MCodeName)
                .DisposeWith(Disposables);

            DeleteCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.DeleteFromQueueAsync(this); });
            MoveUpCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.MoveInQueueAsync(this, i => (short)(i - 1)); });
            MoveDownCommand = ReactiveCommand.CreateFromTask(async () => { await MCodes.MoveInQueueAsync(this, i => (short)(i + 1)); });
        }
    }
}
