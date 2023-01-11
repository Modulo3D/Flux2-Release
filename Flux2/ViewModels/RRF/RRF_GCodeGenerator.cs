using Modulo3DNet;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Flux.ViewModels
{
    /*public class RRF_GCodeLocalVariable<T> : FLUX_GCodeLocalVariable<T>
    {
        public override GCodeString Read => $"var.{Name}";
        public override Func<string, GCodeString> Write => v => $"set var.{Name} = {(typeof(T) == typeof(string) && !$"{v}".Contains('"') ? $"\"{v}\"" : $"{v}")}";
        public override Func<Union<IFLUX_GCodeVariable<T>, T>, GCodeString> Declare => v => $"var {Name} = {(v.IsType1 ? $"{v.Item1}" : typeof(T) == typeof(string) && !$"{v.Item2}".Contains('"') ? $"\"{v.Item2}\"" : v.Item2)}";
        public override string ToString() => Read.ToString();
        public RRF_GCodeLocalVariable(string name) : base(name)
        {
        }
    }
    public class RRF_GCodeGlobalVariable<TData> : FLUX_GCodeGlobalVariable<IRRF_Variable<TData, TData>, TData>
    {
        public override GCodeString Read => $"{Variable}";
        public override Func<string, GCodeString> Write => v => $"set {Variable} = {(typeof(TData) == typeof(string) && !$"{v}".Contains('"') ? $"\"{v}\"" : $"{v}")}"; 
        public override string ToString() => Read.ToString();
        public RRF_GCodeGlobalVariable(IRRF_Variable<TData, TData> variable) : base(variable)
        {
        }
    }
    public class RRF_GCodeLocalArray<T> : FLUX_GCodeLocalArray<T>
    {
        public RRF_GCodeLocalArray(IEnumerable<string> variables) : base(variables)
        { 
        }
        protected override IFLUX_GCodeLocalVariable<T> CreateVariable(string variable_name)
        {
            return new RRF_GCodeLocalVariable<T>(variable_name);
        }
    }
    public class RRF_GCodeGlobalArray<TData> : FLUX_GCodeGlobalArray<IRRF_Array<TData, TData>, IRRF_Variable<TData, TData>, TData>
    {
        public RRF_GCodeGlobalArray(IRRF_Array<TData, TData> variables) : base(variables)
        {
        }
        protected override IFLUX_GCodeGlobalVariable<TData> CreateVariable(IRRF_Variable<TData, TData> variable)
        {
            return new RRF_GCodeGlobalVariable<TData>(variable);
        }
    }

    public class RRF_GCodeGenerator : FLUX_GCodeGenerator<RRF_Connection>
    {
        public RRF_GCodeGenerator(RRF_Connection connection) : base(connection)
        {
        }

        public override GCodeString DeclareLocalArray<T>(string[] array_names, Union<IFLUX_GCodeVariable<T>, T> value, out IFLUX_GCodeLocalArray<T> array)
        {
            array = new RRF_GCodeLocalArray<T>(array_names);
            return array.Foreach((v, i) => v.Declare(value));
        }
        public GCodeString DeclareLocalArray<T>(string array_name, int count, Union<IFLUX_GCodeVariable<T>, T> value, out IFLUX_GCodeLocalArray<T> array)
        {
            return DeclareLocalArray(Enumerable.Range(0, count).Select(i => $"{array_name}_{i}").ToArray(), value, out array);
        }
        public override GCodeString DeclareLocalVariable<T>(string variable_name, Union<IFLUX_GCodeVariable<T>, T> value, out IFLUX_GCodeLocalVariable<T> variable)
        {
            variable = new RRF_GCodeLocalVariable<T>(variable_name);
            return variable.Declare(value);
        }

        public GCodeString DeclareLocalArray<T>(int count, Union<IFLUX_GCodeVariable<T>, T> value, out IFLUX_GCodeLocalArray<T> array, [CallerArgumentExpression("array")] string array_name = null)
        {
            array_name = array_name.Split(" ").LastOrDefault();
            return DeclareLocalArray(array_name, count, value, out array);
        }     
        public GCodeString DeclareLocalVariable<T>(Union<IFLUX_GCodeVariable<T>, T> value, out IFLUX_GCodeLocalVariable<T> variable, [CallerArgumentExpression("variable")] string variable_name = null)
        {
            variable_name = variable_name.Split(" ").LastOrDefault();
            return DeclareLocalVariable(variable_name, value, out variable);
        }


        public override GCodeString DeleteFile(IFLUX_GCodeVariable<string> path)
        {
            return new[]
            {
                $"; deleting file",
                $"M30 {{{path}}}",
            };
        }
        public override GCodeString ExecuteParamacro(IFLUX_GCodeVariable<string> path)
        {
            return new[]
            {
                $"; execute paramacro",
                $"M98 P{{{path}}}",
            };
        }
        public override GCodeString RenameFile(IFLUX_GCodeVariable<string> old_path, IFLUX_GCodeVariable<string> new_path)
        {
            return new[]
            {
                $"; renaming file",
                $"M471 S{{{old_path}}} T{{{new_path}}}",
            };
        }

        public override GCodeString DeleteFile(string folder, string name)
        {
            return new[]
            {
                $"; deleting file {name}",
                $"M30 \"0:/{folder}/{name}\"",
            };
        }
        public override GCodeString RenameFile(string old_path, string new_path)
        {
            return new[]
            {
                $"; renaming file {old_path.Split('/').LastOrDefault()}",
                $"M471 S{old_path} T{new_path} D1",
            };
        }
        public override GCodeString ExecuteParamacro(string folder, string name)
        {
            return new[]
            {
                $"; execute paramacro {name}",
                $"M98 P\"0:/{folder}/{name}\"",
            };
        }
        public override GCodeString AppendFile(string folder, string name, GCodeString source)
        {
            var path = Connection.CombinePaths(folder, name);
            return source.Select(line => $"echo >>\"0:/{path}\" {(line.StartsWith("\"") ? line : $"\"{line.Replace("\"", "\"\"")}\"")}").ToArray();
        }
        public override GCodeString CreateFile(string folder, string name, GCodeString source)
        {
            var path = Connection.CombinePaths(folder, name);
            return GCodeString.Create(
                $"; creating file {name}",
                source.Select((l, i) => $"echo {(i == 0 ? ">" : ">>")}\"0:/{path}\" \"{l.Replace("\"", "\"\"")}\"").ToArray());
        }

        public override GCodeString LogEvent<T>(FluxJob job, T @event)
        {
            if (job.QueuePosition < 0)
                return default;
            return AppendFile(Connection.JobEventPath, $"{job.MCodeKey};{job.JobKey}", $"\"{@event.ToEnumString()};\"^{{state.time}}");
        }
        public override GCodeString LogExtrusion(FluxJob job, ExtrusionKey e, IFLUX_GCodeVariable<double> v)
        {
            if (job.QueuePosition < 0)
                return default;
            return AppendFile(Connection.ExtrusionEventPath, $"{e}", $"\"{job.JobKey};\"^{{{v}}}");
        }

        public override GCodeString GetGlobalArray<TData>(Func<IFLUX_VariableStore, IFLUX_Array<TData, TData>> get_array, out IFLUX_GCodeGlobalArray<TData> array)
        {
            array = new RRF_GCodeGlobalArray<TData>((IRRF_Array<TData, TData>)Connection.GetArray(get_array));
            return default;
        }

        public override GCodeString GetGlobalVariable<TData>(Func<IFLUX_VariableStore, IFLUX_Variable<TData, TData>> get_var, out IFLUX_GCodeGlobalVariable<TData> variable)
        {
            variable = new RRF_GCodeGlobalVariable<TData>((IRRF_Variable<TData, TData>)Connection.GetVariable(get_var));
            return default;
        }
    }*/
}
