using DynamicData;
using Modulo3DNet;
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
        public ISourceList<IConditionViewModel> Conditions { get; private set; }
        public PreparePrintViewModel(FluxViewModel flux) : base(flux)
        {
            SourceListRC.Create(this, v => v.Conditions);
        }
        public override void Initialize()
        {
            var conditions = Flux.StatusProvider.GetConditions<PreparePrintConditionAttribute>().SelectMany(kvp => kvp.Value);
            Conditions.AddRange(conditions.Select(c => c.condition));
        }
    }
}
