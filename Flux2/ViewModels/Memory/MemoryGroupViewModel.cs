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

        [RemoteContent(true)]
        public IObservableCache<IMemoryVariableBase, string> Variables { get; }

        private SourceCache<IMemoryVariableBase, string> VariableSource { get; }

        public MemoryGroupViewModel(FluxViewModel flux, IGrouping<string, IFLUX_VariableBase> group) : base(flux, group.Key)
        {
            VariableName = group.Key;

            VariableSource = new SourceCache<IMemoryVariableBase, string>(vm => vm.VariableBase.Name);
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
                })).AsObservableCacheRC(Disposables);
        }
    }
}
