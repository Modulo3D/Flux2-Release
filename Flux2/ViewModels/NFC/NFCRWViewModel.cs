using DynamicData.Kernel;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class NFCRWViewModel : RemoteControl<NFCRWViewModel>, INFCRWViewModel
    {
        private Optional<CardInfo> _CardInfo;
        public Optional<CardInfo> CardInfo
        {
            get => _CardInfo; 
            set => this.RaiseAndSetIfChanged(ref _CardInfo, value);
        }

        private double _Progress;
        [RemoteOutput(true)]
        public double Progress
        {
            get => _Progress;
            set => this.RaiseAndSetIfChanged(ref _Progress, value);
        }

        private readonly ObservableAsPropertyHelper<string> _CardBrush;
        [RemoteOutput(true)]
        public string CardBrush => _CardBrush.Value;

        private readonly ObservableAsPropertyHelper<Optional<CardId>> _CardId;
        [RemoteOutput(true, typeof(ToStringConverter))]
        public Optional<CardId> CardId => _CardId.Value;

        public NFCRWViewModel() 
        {
            _CardBrush = this.WhenAnyValue(x => x.CardInfo)
                .Select(v => v.HasValue ? FluxColors.Active : FluxColors.Inactive)
                .ToProperty(this, v => v.CardBrush)
                .DisposeWith(Disposables);

            _CardId = this.WhenAnyValue(x => x.CardInfo)
                .Convert(v => v.CardId)
                .ToProperty(this, v => v.CardId)
                .DisposeWith(Disposables);
        }

        public void UpdateCardInfo(Optional<CardInfo> card_info)
        {
            CardInfo = card_info;
        }

        public void UpdateProgress(double progress)
        {
            Progress = progress;
        }
    }
}
