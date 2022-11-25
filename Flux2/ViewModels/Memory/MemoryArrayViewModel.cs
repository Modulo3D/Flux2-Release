using DynamicData;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public abstract class MemoryGroupBaseViewModel<TViewModel> : RemoteControl<TViewModel>
        where TViewModel : MemoryGroupBaseViewModel<TViewModel>
    {
        public FluxViewModel Flux { get; }

        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> ToggleCommand { get; }

        private bool _IsToggled;
        [RemoteOutput(true)]
        public bool IsToggled
        {
            get => _IsToggled;
            set => this.RaiseAndSetIfChanged(ref _IsToggled, value);
        }

        [RemoteOutput(false)]
        public abstract string VariableName { get; }

        public MemoryGroupBaseViewModel(FluxViewModel flux, string name) : base($"{typeof(TViewModel).GetRemoteControlName()}??{name}")
        {
            Flux = flux;
            ToggleCommand = ReactiveCommand.Create(Toggle);
        }

        private void Toggle()
        {
            IsToggled = !IsToggled;
        }
    }

    public class MemoryArrayViewModel : MemoryGroupBaseViewModel<MemoryArrayViewModel>, IMemoryVariableBase
    {
        public IFLUX_Array Array { get; }
        public IFLUX_VariableBase VariableBase => Array;

        [RemoteContent(true)]
        public IObservableCache<MemoryVariableViewModel, VariableAlias> Variables { get; }

        [RemoteOutput(false)]
        public override string VariableName => Array.Name;

        public MemoryArrayViewModel(FluxViewModel flux, IFLUX_Array plc_array) : base(flux, plc_array.Name)
        {
            Array = plc_array;
            Variables = Array.Variables.Connect()
                .Transform(v => new MemoryVariableViewModel(Flux, v))
                .Filter(this.WhenAnyValue(v => v.IsToggled).Select(t =>
                {
                    bool filter(MemoryVariableViewModel v) => t;
                    return (Func<MemoryVariableViewModel, bool>)filter;
                })).AsObservableCache();
        }
    }
}
