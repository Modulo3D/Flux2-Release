using DynamicData.Kernel;
using Flux.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public abstract class ConditionAttribute : Attribute
    {
        public ConditionAttribute()
        {
        }
    }
    public class FilterConditionAttribute : ConditionAttribute
    {
        public bool FilterOnCycle { get; }
        public Optional<string> Name { get; }
        public Optional<string[]> IncludeAlias { get; }
        public Optional<string[]> ExcludeAlias { get; }
        public FilterConditionAttribute(string name = default, bool filter_on_cycle = true, string[] include_alias = default, string[] exclude_alias = default)
        {
            Name = name;
            IncludeAlias = include_alias;
            ExcludeAlias = exclude_alias;
            FilterOnCycle = filter_on_cycle;
            if (include_alias != null && exclude_alias != null)
                throw new ArgumentException("non è possibile aggiungere alias di inclusione ed esclusione allo stesso momento");
        }
        public bool Filter(IConditionViewModel condition)
        {
            if (IncludeAlias.HasValue)
                return IncludeAlias.Value.Contains(condition.Name);
            if (ExcludeAlias.HasValue)
                return !ExcludeAlias.Value.Contains(condition.Name);
            return true;
        }
    }
    public class CycleConditionAttribute : FilterConditionAttribute
    {
        public CycleConditionAttribute(string name = default, bool filter_on_cycle = true, string[] include_alias = default, string[] exclude_alias = default)
            : base(name, filter_on_cycle, include_alias, exclude_alias)
        {
        }
    }
    public class PrintConditionAttribute : FilterConditionAttribute
    {
        public PrintConditionAttribute(string name = default, bool filter_on_cycle = true, string[] include_alias = default, string[] exclude_alias = default)
             : base(name, filter_on_cycle, include_alias, exclude_alias)
        {
        }
    }
}
