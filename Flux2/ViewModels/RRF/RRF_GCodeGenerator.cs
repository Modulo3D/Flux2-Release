using Modulo3DNet;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Flux.ViewModels
{
    public class RRF_GCodeGenerator : FLUX_GCodeGenerator<RRF_Connection>
    {
        public RRF_GCodeGenerator(RRF_Connection connection) : base(connection)
        {
        }

        public override GCodeString DeclareGlobalArray<T>(string[] array_names, out GCodeArray<T> array)
        {
            array = new GCodeArray<T>()
            {
                Variables = array_names.Select(name =>
                {
                    DeclareGlobalVariable<T>(name, out var variable);
                    return variable;
                }).ToArray(),
            };
            return array.Foreach((v, i) => v.Declare);
        }
        public override GCodeString DeclareLocalArray<T>(string[] array_names, Union<GCodeVariable<T>, T> value, out GCodeArray<T> array)
        {
            array = new GCodeArray<T>()
            {
                Variables = array_names.Select(name =>
                {
                    DeclareLocalVariable(name, value, out var variable);
                    return variable;
                }).ToArray(),
            };
            return array.Foreach((v, i) => v.Declare);
        }
        public GCodeString DeclareGlobalArray<T>(string array_name, int count, out GCodeArray<T> array)
        {
            return DeclareGlobalArray(Enumerable.Range(0, count).Select(i => $"{array_name}_{i}").ToArray(), out array);
        }
        public GCodeString DeclareLocalArray<T>(string array_name, int count, Union<GCodeVariable<T>, T> value, out GCodeArray<T> array)
        {
            return DeclareLocalArray(Enumerable.Range(0, count).Select(i => $"{array_name}_{i}").ToArray(), value, out array);
        }
        public override GCodeString DeclareGlobalVariable<T>(string variable_name, out GCodeVariable<T> variable)
        {
            variable = new GCodeVariable<T>()
            {
                Name = variable_name,
                Read = $"global.{variable_name}",
                Declare = new[]
                {
                    $"if !exists(global.{variable_name})",
                    $"    global {variable_name} = {(typeof(T) == typeof(string) ? $"\"{default(T)}\"" : default(T))}",
                },
                Write = v => $"set global.{variable_name} = {(typeof(T) == typeof(string) && !$"{v}".Contains('"') ? $"\"{v}\"" : $"{v}")}",
            };
            return variable.Declare;
        }
        public override GCodeString DeclareLocalVariable<T>(string variable_name, Union<GCodeVariable<T>, T> value, out GCodeVariable<T> variable)
        {
            variable = new GCodeVariable<T>()
            {
                Name = variable_name,
                Read = $"var.{variable_name}",
                Write = v => $"set var.{variable_name} = {(typeof(T) == typeof(string) && !$"{v}".Contains('"') ? $"\"{v}\"" : $"{v}")}",
                Declare = $"var {variable_name} = {(value.IsType1 ? $"{value.Item1}" : typeof(T) == typeof(string) && !$"{value.Item2}".Contains('"') ? $"\"{value.Item2}\"" : value.Item2)}",
            };
            return variable.Declare;
        }

        public GCodeString DeclareGlobalArray<T>(int count, out GCodeArray<T> array, [CallerArgumentExpression("array")] string array_name = null)
        {
            array_name = array_name.Split(" ").LastOrDefault();
            return DeclareGlobalArray(array_name, count, out array);
        }
        public GCodeString DeclareLocalArray<T>(int count, Union<GCodeVariable<T>, T> value, out GCodeArray<T> array, [CallerArgumentExpression("array")] string array_name = null)
        {
            array_name = array_name.Split(" ").LastOrDefault();
            return DeclareLocalArray(array_name, count, value, out array);
        }
        public GCodeString DeclareGlobalVariable<T>(out GCodeVariable<T> variable, [CallerArgumentExpression("variable")] string variable_name = null)
        {
            variable_name = variable_name.Split(" ").LastOrDefault();
            return DeclareGlobalVariable(variable_name, out variable);
        }
        public GCodeString DeclareLocalVariable<T>(Union<GCodeVariable<T>, T> value, out GCodeVariable<T> variable, [CallerArgumentExpression("variable")] string variable_name = null)
        {
            variable_name = variable_name.Split(" ").LastOrDefault();
            return DeclareLocalVariable(variable_name, value, out variable);
        }


        public override GCodeString DeleteFile(GCodeVariable<string> path)
        {
            return new[]
            {
                $"; deleting file {path.Name}",
                $"M30 {{{path}}}",
            };
        }
        public override GCodeString ExecuteParamacro(GCodeVariable<string> path)
        {
            return new[]
            {
                $"; execute paramacro {path.Name}",
                $"M98 P{{{path}}}",
            };
        }
        public override GCodeString RenameFile(GCodeVariable<string> old_path, GCodeVariable<string> new_path)
        {
            return new[]
            {
                $"; renaming file {old_path.Name}",
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

        public override GCodeString LogEvent<T>(Job job, T @event)
        {
            if (job.QueuePosition < 0)
                return default;
            return AppendFile(Connection.JobEventPath, $"{job.MCodeKey};{job.JobKey}", $"\"{@event.ToEnumString()};\"^{{state.time}}");
        }
        public override GCodeString LogExtrusion(Job job, ExtrusionKey e, GCodeVariable<double> v)
        {
            if (job.QueuePosition < 0)
                return default;
            return AppendFile(Connection.ExtrusionEventPath, $"{e}", $"\"{job.JobKey};\"^{{{v}}}");
        }
    }
}
