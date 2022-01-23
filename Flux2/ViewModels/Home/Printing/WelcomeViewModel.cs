using Modulo3DStandard;
using ReactiveUI;
using System.Reactive;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class WelcomeViewModel : HomePhaseViewModel<WelcomeViewModel>
    {
        [RemoteCommand]
        public ReactiveCommand<Unit, Unit> SelectPartProgramCommand { get; }

        public WelcomeViewModel(FluxViewModel flux) : base(flux, "welcome")
        {
            SelectPartProgramCommand = ReactiveCommand.CreateFromTask(SelectFileAsync);
        }

        public Task SelectFileAsync()
        {
            Flux.Navigator.Navigate(Flux.MCodes);
            return Task.CompletedTask;
        }
    }
}
