using DynamicData;
using Modulo3DNet;
using System.Linq;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class MemoryViewModel : FluxRoutableViewModel<MemoryViewModel>
    {
        public SourceCache<MemoryGroupViewModel, string> VariableGroupsSource { get; private set; }

        [RemoteContent(true)]
        public IObservableCache<MemoryGroupViewModel, string> VariableGroups { get; }

        public MemoryViewModel(FluxViewModel flux) : base(flux)
        {
            SourceCacheRC.Create(this, v => v.VariableGroupsSource, vm => vm.VariableName);
            VariableGroupsSource.Edit(innerList =>
            {
                var variable_store = flux.ConnectionProvider.VariableStoreBase;
                var variables = variable_store.Variables;

                var groups = variables.Values
                    .GroupBy(v => v.Group)
                    .Select(group => new MemoryGroupViewModel(Flux, group));

                innerList.Clear();
                innerList.AddOrUpdate(groups);
            });

            VariableGroups = VariableGroupsSource.Connect()
                .AsObservableCacheRC(this);
        }
    }
}
