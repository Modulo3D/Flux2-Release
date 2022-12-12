using Modulo3DNet;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Flux.ViewModels
{
    public class OSAI_GCodeLocalVariable<T> : FLUX_GCodeLocalVariable<T>
    {
        public override GCodeString Read => Name;
        public override Func<string, GCodeString> Write => v => $"{Name} = {(typeof(T) == typeof(string) && !$"{v}".Contains('"') ? $"\"{v}\"" : $"{v}")}";
        public override Func<Union<IFLUX_GCodeVariable<T>, T>, GCodeString> Declare => v => $"{Name} = {(!v.IsType1 && typeof(T) == typeof(string) && !$"{v.Item2}".Contains('"') ? $"\"{v}\"" : v)}";
        public OSAI_GCodeLocalVariable(string name) : base(name)
        {
        }
    }
    public class OSAI_GCodeLocalArray<T> : FLUX_GCodeLocalArray<T>
    {
        public OSAI_GCodeLocalArray(IEnumerable<string> variables) : base(variables)
        {
        }
        protected override IFLUX_GCodeLocalVariable<T> CreateVariable(string variable_name)
        {
            return new OSAI_GCodeLocalVariable<T>(variable_name);
        }
    }
    public class OSAI_GCodeGenerator : FLUX_GCodeGenerator<OSAI_Connection>
    {
        public OSAI_GCodeGenerator(OSAI_Connection connection) : base(connection)
        {
        }

        // FILE
        public override GCodeString LogEvent<T>(FluxJob job, T @event)
        {
            if (job.QueuePosition < 0)
                return default;
            var _event_path = Connection.CombinePaths(Connection.JobEventPath, $"{job.MCodeKey};{job.JobKey}");
            return GCodeString.Create(
            // save event
                ReadDateTime(out var date_time),
                DeclareLocalVariable(0, _event_path, out var event_path),
                AppendFile((object)event_path, $"{@event.ToEnumString()};", (object)date_time));
        }
        public override GCodeString DeleteFile(IFLUX_GCodeVariable<string> path)
        {
            return $"(DEL, ?{path})";
        }
        public override GCodeString DeleteFile(string folder, string name)
        {
            return $"(DEL, {Connection.CombinePaths(folder, name)})";
        }
        public override GCodeString RenameFile(string old_path, string new_path)
        {
            return $"(REN, {old_path}, {new_path})";
        }
        public override GCodeString AppendFile(string folder, string name, GCodeString source)
        {
            return GCodeString.Create(
                $"(OPN, 1, \"{Connection.CombinePaths(folder, name)}\", A, A)",
                source.Select(line => $"(WRT, 1, \"{line}\")").ToArray(),
                "(CLO, 1)");
        }
        public override GCodeString CreateFile(string folder, string name, GCodeString source)
        {
            return GCodeString.Create(
                $"(OPN, 1, \"{Connection.CombinePaths(folder, name)}\", A, W)",
                source.Select(line => $"(WRT, 1, \"{line}\")").ToArray(),
                "(CLO, 1)");
        }
        public override GCodeString LogExtrusion(FluxJob job, ExtrusionKey e, IFLUX_GCodeVariable<double> v)
        {
            throw new NotImplementedException();
        }
        public override GCodeString RenameFile(IFLUX_GCodeVariable<string> old_path, IFLUX_GCodeVariable<string> new_path)
        {
            return $"(REN, ?{old_path}, ?{new_path})";
        }
        public GCodeString AppendFile(Union<(string folder, string name), object> path, params Union<GCodeString, object>[] source)
        {
            return new GCodeString(append_file());

            IEnumerable<GCodeString> append_file()
            {
                var _path = path.IsType1 ?
                    $"\"{Connection.CombinePaths(path.Item1.folder, path.Item1.name)}\"" :
                    $"?{path.Item2}";

                yield return $"(OPN, 1, {_path}, A, A)";
                yield return $"(WRT, 1, {string.Join(", ", source.Select(s => s.IsType1 ? $"\"{s.Item}\"" : $"{s.Item2}"))})";
                yield return "(CLO, 1)";
            }
        }
        public GCodeString CreateFile(Union<(string folder, string name), object> path, params Union<GCodeString, object>[] source)
        {
            return new GCodeString(append_file());

            IEnumerable<GCodeString> append_file()
            {
                var _path = path.IsType1 ?
                    $"\"{Connection.CombinePaths(path.Item1.folder, path.Item1.name)}\"" :
                    $"?{path.Item2}";

                yield return $"(OPN, 1, {_path}, A, W)";
                yield return $"(WRT, 1, {string.Join(", ", source.Select(s => s.IsType1 ? $"\"{s.Item}\"" : $"{s.Item2}"))})";
                yield return "(CLO, 1)";
            }
        }

        // VARIABLE
        public GCodeString DeclareLocalVariable(uint address, Union<IFLUX_GCodeVariable<double>, double> value, out IFLUX_GCodeLocalVariable<double> variable)
        {
            return DeclareLocalVariable($"E{address}", value, out variable);
        }
        public override GCodeString DeclareLocalVariable<T>(string variable_name, Union<IFLUX_GCodeVariable<T>, T> value, out IFLUX_GCodeLocalVariable<T> gcode_variable)
        {
            gcode_variable = new OSAI_GCodeLocalVariable<T>(variable_name);
            return gcode_variable.Declare(value);
        }
        public GCodeString DeclareLocalVariable(Union<uint, (uint index, uint lenght)> address, Union<IFLUX_GCodeVariable<string>, string> value, out IFLUX_GCodeLocalVariable<string> variable)
        {
            var variable_name = address.Item switch
            {
                uint index => $"LS{index}",
                (uint index, uint lenght) => $"SC{index}.{lenght}",
                _ => default
            };

            if (string.IsNullOrEmpty(variable_name))
                throw new ArgumentException();

            return DeclareLocalVariable(variable_name, value, out variable);
        }
        
        // ARRAY
        public override GCodeString DeclareLocalArray<T>(string[] variable_names, Union<IFLUX_GCodeVariable<T>, T> value, out IFLUX_GCodeLocalArray<T> gcode_array)
        {
            gcode_array = new OSAI_GCodeLocalArray<T>(variable_names);
            return gcode_array.Foreach((v, i) => v.Declare(value));
        }
      
        /*public GCodeString DeclareGlobalVariable<T>(OSAI_VARCODE varcode, Union<uint, (uint index, uint lenght)> address, IOSAI_AddressVariable address_variable, out GCodeVariable<T> gcode_variable)
        {
            var variable_name = (varcode, address.Item) switch
            {
                (OSAI_VARCODE.E_CODE, uint index) => $"E{index}",
                (OSAI_VARCODE.LS_CODE, uint index) => $"LS{address}",
                (OSAI_VARCODE.SC_CODE, (uint index, uint lenght)) => $"SC{address}.{lenght}",
                _ => default
            };

            if (string.IsNullOrEmpty(variable_name))
                throw new ArgumentException();

            var update = address_variable.Address switch
            {
                OSAI_IndexAddress index_address => index_address.VarCode switch
                {
                    OSAI_VARCODE.GD_CODE => $"M4000[0, {index_address.Index}, 0, {address}]",
                    OSAI_VARCODE.MW_CODE => $"M4000[1, {index_address.Index}, 0, {address}]",
                    OSAI_VARCODE.MD_CODE => $"M4000[3, {index_address.Index}, 0, {address}]",
                    OSAI_VARCODE.GW_CODE => $"M4000[4, {index_address.Index}, 0, {address}]",
                    OSAI_VARCODE.L_CODE => $"M4000[6, {index_address.Index}, 0, {address}]",
                    OSAI_VARCODE.AA_CODE => $"M4000[7, {index_address.Index}, 0, {address}]",
                    _ => default
                },
                OSAI_BitIndexAddress bit_index_address => bit_index_address.VarCode switch
                {
                    OSAI_VARCODE.MW_CODE => $"M4000[2, {bit_index_address.Index}, {bit_index_address.BitIndex}, {address}]",
                    OSAI_VARCODE.GW_CODE => $"M4000[5, {bit_index_address.Index}, {bit_index_address.BitIndex}, {address}]",
                    _ => default
                },
                OSAI_NamedAddress named_address => $"{variable_name} = {named_address.Name}({named_address.Index})",
                _ => default
            };

            if (string.IsNullOrEmpty(update))
                throw new ArgumentException();

            GCodeString write(string v)
            {
                var write = address_variable.Address switch
                {
                    OSAI_IndexAddress index_address => index_address.VarCode switch
                    {
                        OSAI_VARCODE.GD_CODE => $"M4001[0, {address_variable.Address.Index}, 0, {address}]",
                        OSAI_VARCODE.MW_CODE => $"M4001[1, {address_variable.Address.Index}, 0, {address}]",
                        OSAI_VARCODE.MD_CODE => $"M4001[3, {address_variable.Address.Index}, 0, {address}]",
                        OSAI_VARCODE.GW_CODE => $"M4001[4, {address_variable.Address.Index}, 0, {address}]",
                        OSAI_VARCODE.L_CODE => $"M4001[6, {address_variable.Address.Index}, 0, {address}]",
                        OSAI_VARCODE.AA_CODE => $"M4001[7, {address_variable.Address.Index}, 0, {address}]",
                        _ => default
                    },
                    OSAI_BitIndexAddress bit_index_address => bit_index_address.VarCode switch
                    {
                        OSAI_VARCODE.MW_CODE => $"M4000[2, {bit_index_address.Index}, {bit_index_address.BitIndex}, {address}]",
                        OSAI_VARCODE.GW_CODE => $"M4000[5, {bit_index_address.Index}, {bit_index_address.BitIndex}, {address}]",
                        _ => default
                    },
                    OSAI_NamedAddress named_address => $"{variable_name} = {named_address.Name}({named_address.Index})",
                    _ => default

                };

                if (string.IsNullOrEmpty(write))
                    throw new ArgumentException();

                return GCodeString.Create($"{variable_name} = {(typeof(T) == typeof(string) && !$"{v}".Contains('"') ? $"\"{v}\"" : $"{v}")}", write);
            }

            gcode_variable = new GCodeVariable<T>()
            {
                Declare = default,
                Write = write,
                Update = update,
                Name = variable_name,
                Read = variable_name,
            };

            return gcode_variable.Declare;
        }
        public GCodeString DeclareGlobalVariable<TRData, TWData>(OSAI_VARCODE varcode, Union<uint, (uint index, uint lenght)> address, Func<OSAI_VariableStore, IFLUX_Variable<TRData, TWData>> get_variable, out GCodeVariable<TRData> gcode_variable)
        {
            var variable = Connection.GetVariable(get_variable);
            if (variable is not IOSAI_AddressVariable address_variable)
                throw new ArgumentException();
            return DeclareGlobalVariable(varcode, address, address_variable, out gcode_variable);
        }
        public GCodeString DeclareGlobalArray(uint address, Func<OSAI_VariableStore, IFLUX_Array<double, double>> get_variable, out GCodeArray<double> gcode_variable)
        {
            return DeclareGlobalArray(OSAI_VARCODE.E_CODE, address, get_variable, out gcode_variable);
        }
        public GCodeString DeclareGlobalArray<TRData, TWData>(OSAI_VARCODE varcode, uint address, Func<OSAI_VariableStore, IFLUX_Array<TRData, TWData>> get_array, out GCodeArray<TRData> gcode_array)
        {
            var array = Connection.GetArray(get_array);

            gcode_array = new GCodeArray<TRData>()
            {
                Variables = array.Variables.Items.Select((variable, i) =>
                {
                    if (variable is not IOSAI_AddressVariable address_variable)
                        throw new ArgumentException();
                    DeclareGlobalVariable<TRData>(varcode, (uint)(address + i), address_variable, out var gcode_variable);
                    return gcode_variable;
                }).ToArray()
            };

            return gcode_array.Foreach((v, i) => v.Declare);
        }
        */

        // MACRO
        public override GCodeString ExecuteParamacro(IFLUX_GCodeVariable<string> path)
        {
            return $"(CLS, ?{path})";
        }
        public override GCodeString ExecuteParamacro(string folder, string name)
        {
            return $"(CLS, {Connection.CombinePaths(folder, name)})";
        }

        // STRING
        public GCodeString ReadDateTime(out IFLUX_GCodeLocalVariable<string> date_time)
        {
            DeclareLocalVariable((0, 22), "", out date_time);
            return GCodeString.Create(
                $"(GDT, D2, T0, SC0.10, SC11.11)",
                "SC10.1 = \";\"");
        }
        public GCodeString ToString<T>(IFLUX_GCodeVariable<T> variable, uint address, out IFLUX_GCodeLocalVariable<string> str_variable)
        {
            str_variable = default;
            if (typeof(T) == typeof(string))
                throw new ArgumentException();

            DeclareLocalVariable(address, "", out str_variable);
            return $"(N2S, \"%d\", {variable}, {str_variable})";
        }
        public GCodeString AppendString(IFLUX_GCodeVariable<string> var, params Union<GCodeString, IFLUX_GCodeVariable<string>>[] strings)
        {
            return new GCodeString(append_string());

            IEnumerable<string> append_string()
            {
                foreach (var @string in strings)
                {
                    var _string = @string.IsType1 ?
                        @string.Item1.Select(l => $"(CAT, \"{l}\", {var})") :
                        new[] { $"(CAT, {@string.Item2}, {var})" };

                    foreach (var __string in _string)
                        yield return __string;
                }
            }
        }

        public override GCodeString GetGlobalArray<TRData, TWData>(Func<IFLUX_VariableStore, IFLUX_Array<TRData, TWData>> get_array, out IFLUX_GCodeGlobalArray<TRData, TWData> array)
        {
            throw new NotImplementedException();
        }
        public override GCodeString GetGlobalVariable<TRData, TWData>(Func<IFLUX_VariableStore, IFLUX_Variable<TRData, TWData>> get_var, out IFLUX_GCodeGlobalVariable<TRData, TWData> variable)
        {
            throw new NotImplementedException();
        }
    }
}
