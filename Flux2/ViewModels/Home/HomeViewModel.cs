﻿using Modulo3DNet;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public interface IHomePhaseViewModel : IRemoteControl
    {
        FluxViewModel Flux { get; }
        ReactiveCommandBaseRC<Unit, Unit> CancelPrintCommand { get; }
    }

    public abstract class HomePhaseViewModel<THomePhase> : RemoteControl<THomePhase>, IHomePhaseViewModel
        where THomePhase : HomePhaseViewModel<THomePhase>, IHomePhaseViewModel
    {
        public FluxViewModel Flux { get; }
        [RemoteCommand]
        public ReactiveCommandBaseRC<Unit, Unit> CancelPrintCommand { get; }
        public HomePhaseViewModel(FluxViewModel flux)
        {
            Flux = flux;
            var can_cancel = Flux.StatusProvider.WhenAnyValue(s => s.StatusEvaluation).Select(s => s.CanSafeStop);
            CancelPrintCommand = ReactiveCommandBaseRC.CreateFromTask(async () => { await Flux.ConnectionProvider.CancelPrintAsync(false); }, (THomePhase)this, can_cancel);
        }
        public virtual void Initialize()
        {
        }
    }

    public class HomeViewModel : FluxRoutableNavBarViewModel<HomeViewModel>, IRemoteControl
    {
        public WelcomeViewModel WelcomePhase { get; }
        public PrintingViewModel PrintingPhase { get; }
        public LowNozzlesViewModel LowNozzlesPhase { get; }
        public PurgeNozzlesViewModel ColdNozzlesPhase { get; }
        public PreparePrintViewModel PreparePrintPhase { get; }
        public InvalidNozzlesViewModel InvalidToolsPhase { get; }
        public LowMaterialsViewModel LowMaterialsPhase { get; }
        public InvalidProbesViewModel InvalidProbesPhase { get; }
        public InvalidPrinterViewModel InvalidPrinterPhase { get; }
        public InvalidMaterialsViewModel InvalidMaterialsPhase { get; }

        private readonly ObservableAsPropertyHelper<IHomePhaseViewModel> _HomePhase;
        [RemoteContent(true)]
        public IHomePhaseViewModel HomePhase => _HomePhase.Value;

        public HomeViewModel(FluxViewModel flux) : base(flux)
        {
            WelcomePhase = new WelcomeViewModel(Flux);
            PrintingPhase = new PrintingViewModel(Flux);
            LowNozzlesPhase = new LowNozzlesViewModel(Flux);
            ColdNozzlesPhase = new PurgeNozzlesViewModel(Flux);
            PreparePrintPhase = new PreparePrintViewModel(Flux);
            InvalidToolsPhase = new InvalidNozzlesViewModel(Flux);
            LowMaterialsPhase = new LowMaterialsViewModel(Flux);
            InvalidProbesPhase = new InvalidProbesViewModel(Flux);
            InvalidPrinterPhase = new InvalidPrinterViewModel(Flux);
            InvalidMaterialsPhase = new InvalidMaterialsViewModel(Flux);

            PrintingPhase.Initialize();
            LowNozzlesPhase.Initialize();
            ColdNozzlesPhase.Initialize();
            PreparePrintPhase.Initialize();
            InvalidToolsPhase.Initialize();
            LowMaterialsPhase.Initialize();
            InvalidProbesPhase.Initialize();
            InvalidMaterialsPhase.Initialize();

            _HomePhase = Observable.CombineLatest(
                Flux.StatusProvider.WhenAnyValue(v => v.StatusEvaluation),
                Flux.StatusProvider.WhenAnyValue(v => v.StartEvaluation),
                Flux.StatusProvider.WhenAnyValue(v => v.PrintingEvaluation),
                GetHomeViewModel)
                .Throttle(TimeSpan.FromSeconds(0.25))
                .ToProperty(this, h => h.HomePhase);
        }

        private IHomePhaseViewModel GetHomeViewModel(StatusEvaluation status_eval, StartEvaluation start_eval, PrintingEvaluation printing_eval)
        {
            if (!printing_eval.MCode.HasValue)
                return WelcomePhase;

            if (start_eval.HasInvalidPrinter)
                return InvalidPrinterPhase;

            if (start_eval.HasInvalidTools)
                return InvalidToolsPhase;

            if (start_eval.HasInvalidMaterials)
                return InvalidMaterialsPhase;

            if (start_eval.HasInvalidProbes)
                return InvalidProbesPhase;

            if (!status_eval.IsCycle)
            {
                if (start_eval.HasLowMaterials)
                    if (!start_eval.StartWithLowMaterials)
                        return LowMaterialsPhase;

                if (start_eval.HasLowNozzles)
                    if (!start_eval.StartWithLowNozzles)
                        return LowNozzlesPhase;

                if (!status_eval.CanSafePrint)
                    return PreparePrintPhase;
            }

            if (printing_eval.Recovery.HasValue)
                if (start_eval.HasColdNozzles)
                    return ColdNozzlesPhase;

            return PrintingPhase;
        }
    }
}
