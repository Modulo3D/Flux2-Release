using Modulo3DNet;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class WelcomeViewModel : HomePhaseViewModel<WelcomeViewModel>
    {
        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> SelectPartProgramCommand { get; }

        private readonly ObservableAsPropertyHelper<string> _PrinterName;
        [RemoteOutput(true)]
        public string PrinterName => _PrinterName.Value;

        public WelcomeViewModel(FluxViewModel flux) : base(flux)
        {
            SelectPartProgramCommand = ReactiveCommandBaseRC.CreateFromTask(SelectFileAsync, this);

            _PrinterName = Flux.SettingsProvider
                .WhenAnyValue(s => s.Printer)
                .ConvertOr(p => p.Name, () => "")
                .ToProperty(this, v => v.PrinterName)
                .DisposeWith(Disposables);
        }

        public Task SelectFileAsync()
        {
            Flux.Navigator.Navigate(Flux.MCodes);
            return Task.CompletedTask;
        }
    }
}
