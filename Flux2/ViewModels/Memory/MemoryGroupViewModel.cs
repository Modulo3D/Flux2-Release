using DynamicData;
using DynamicData.Kernel;
using DynamicData.PLinq;
using Modulo3DNet;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Flux.ViewModels
{
    public class MemoryGroupViewModel : MemoryGroupBaseViewModel<MemoryGroupViewModel>
    {
        [RemoteOutput(false)]
        public override string VariableName { get; }

        [RemoteContent(true, comparer: (nameof(VariableName)))]
        public IObservableCache<IMemoryVariableBase, string> Variables { get; private set; }

        private SourceCache<IMemoryVariableBase, string> VariableSource { get; set; }

        public MemoryGroupViewModel(FluxViewModel flux, IGrouping<string, IFLUX_VariableBase> group) : base(flux, group.Key)
        {
            VariableName = group.Key;

            SourceCacheRC.Create(this, v => v.VariableSource, vm => vm.VariableBase.Name);
            VariableSource.Edit(innerList =>
            {
                innerList.Clear();
                foreach (var var_base in group)
                {
                    var variable_store = flux.ConnectionProvider.VariableStoreBase;
                    var attributes = variable_store.Attributes.Lookup(var_base.Name);

                    switch (var_base)
                    {
                        case IFLUX_Array array:
                            innerList.AddOrUpdate(new MemoryArrayViewModel(Flux, array, attributes));
                            break;
                        case IFLUX_Variable variable:
                            innerList.AddOrUpdate(new MemoryVariableViewModel(Flux, variable, attributes));
                            break;
                    }
                }
            });

            Variables = VariableSource.Connect()
                .Filter(this.WhenAnyValue(v => v.IsToggled).Select(t =>
                {
                    bool filter(IMemoryVariableBase v) => t;
                    return (Func<IMemoryVariableBase, bool>)filter;
                })).AsObservableCacheRC(this);
        }
    }
}
