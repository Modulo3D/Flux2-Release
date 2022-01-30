using DynamicData;
using Modulo3DStandard;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.IO;
using System;
using DynamicData.Kernel;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Threading;

namespace Flux.ViewModels
{
    public class MemoryViewModel : FluxRoutableViewModel<MemoryViewModel>
    {
        public SourceCache<MemoryGroupViewModel, string> VariableGroupsSource { get; }
        
        [RemoteContent(true)]
        public IObservableCache<MemoryGroupViewModel, string> VariableGroups { get; }

        public MemoryViewModel(FluxViewModel flux) : base(flux)
        {
            VariableGroupsSource = new SourceCache<MemoryGroupViewModel, string>(vm => vm.VariableName);
            VariableGroupsSource.Edit(innerList =>
            {
                var memory = flux.ConnectionProvider.VariableStore;
                var groups = memory.Variables.Values
                    .GroupBy(v => v.Group)
                    .Select(group => new MemoryGroupViewModel(Flux, group));

                innerList.Clear();
                innerList.AddOrUpdate(groups);
            });

            VariableGroups = VariableGroupsSource.Connect()
                .AsObservableCache();
        }
    }
}
