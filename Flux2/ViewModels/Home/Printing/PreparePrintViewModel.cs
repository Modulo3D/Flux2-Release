using DynamicData;
using Modulo3DStandard;
using ReactiveUI;
using System.Linq;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class PreparePrintConditionAttribute : FilterConditionAttribute
    {
        public PreparePrintConditionAttribute(string name = default, bool filter_on_cycle = true, string[] include_alias = default, string[] exclude_alias = default)
            : base(name, filter_on_cycle, include_alias, exclude_alias)
        {
        }
    }
    public class PreparePrintViewModel : HomePhaseViewModel<PreparePrintViewModel>
    {
        [RemoteContent(true)]
        public ISourceCache<IConditionViewModel, string> Conditions { get; private set; }
        public PreparePrintViewModel(FluxViewModel flux) : base(flux)
        {
            Conditions = new SourceCache<IConditionViewModel, string>(c => c.Name);
        }
        public override void Initialize()
        {
            var conditions = Flux.StatusProvider.GetConditions<PreparePrintConditionAttribute>().SelectMany(kvp => kvp.Value);
            Conditions.AddOrUpdate(conditions.Select(c => c.condition));
            InitializeRemoteView();
        }
    }
}
