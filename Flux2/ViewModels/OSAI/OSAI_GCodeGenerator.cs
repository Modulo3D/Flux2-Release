using Modulo3DNet;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Flux.ViewModels
{
    public class OSAI_GCodeGenerator : FLUX_GCodeGenerator<OSAI_Connection>
    {
        public OSAI_GCodeGenerator(OSAI_Connection connection) : base(connection)
        {
        }

        // FILE
        public override GCodeString LogEvent<T>(Job job, T @event)
        {
            if (job.QueuePosition < 0)
                return default;

            var _event_path = Connection.CombinePaths(Connection.JobEventPath, $"{job.MCodeKey};{job.JobKey}");
            return GCodeString.Create(
                // save event
                ReadDateTime(out var date_time),
                DeclareLocalVariable(0, _event_path, out var event_path),
                AppendFile(event_path, $"{@event.ToEnumString()};", date_time));
        }
        public override GCodeString DeleteFile(GCodeVariable<string> path)
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
        public override GCodeString LogExtrusion(Job job, ExtrusionKey e, GCodeVariable<double> v)
        {
            throw new NotImplementedException();
        }
        public override GCodeString RenameFile(GCodeVariable<string> old_path, GCodeVariable<string> new_path)
        {
            return $"(REN, ?{old_path}, ?{new_path})";
        }
        public GCodeString AppendFile(Union<(string folder, string name), GCodeVariable<string>> path, params Union<string, GCodeVariable<string>>[] source)
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
        public GCodeString CreateFile(Union<(string folder, string name), GCodeVariable<string>> path, params Union<GCodeString, GCodeVariable<string>>[] source)
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
        public override GCodeString DeclareGlobalVariable<T>(string variable_name, out GCodeVariable<T> variable)
        {
            throw new NotImplementedException();
        }
        public GCodeString DeclareLocalVariable(uint address, Union<GCodeVariable<double>, double> value, out GCodeVariable<double> variable)
        {
            return DeclareLocalVariable($"E{address}", value, out variable);
        }
        public override GCodeString DeclareLocalVariable<T>(string variable_name, Union<GCodeVariable<T>, T> value, out GCodeVariable<T> gcode_variable)
        {
            gcode_variable = new GCodeVariable<T>()
            {
                Name = variable_name,
                Read = variable_name,
                Write = v => $"{variable_name} = {(typeof(T) == typeof(string) && !$"{v}".Contains('"') ? $"\"{v}\"" : $"{v}")}",
                Declare = $"{variable_name} = {(!value.IsType1 && typeof(T) == typeof(string) && !$"{value.Item2}".Contains('"') ? $"\"{value}\"" : value)}",
            };
            return gcode_variable.Declare;
        }
        public GCodeString DeclareLocalVariable(Union<uint, (uint index, uint lenght)> address, Union<GCodeVariable<string>, string> value, out GCodeVariable<string> variable)
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
        public GCodeString DeclareGlobalVariable(uint address, Func<OSAI_VariableStore, IFLUX_Variable<string, string>> get_variable, out GCodeVariable<string> gcode_variable)
        {
            return DeclareGlobalVariable(OSAI_VARCODE.LS_CODE, address, get_variable, out gcode_variable);
        }
        public GCodeString DeclareGlobalVariable(uint address, Func<OSAI_VariableStore, IFLUX_Variable<double, double>> get_variable, out GCodeVariable<double> gcode_variable)
        {
            return DeclareGlobalVariable(OSAI_VARCODE.E_CODE, address, get_variable, out gcode_variable);
        }
        public GCodeString DeclareGlobalVariable(uint address, uint lenght, Func<OSAI_VariableStore, IFLUX_Variable<string, string>> get_variable, out GCodeVariable<string> gcode_variable)
        {
            return DeclareGlobalVariable(OSAI_VARCODE.SC_CODE, (address, lenght), get_variable, out gcode_variable);
        }
        public GCodeString DeclareGlobalVariable(uint address, Func<OSAI_VariableStore, IFLUX_Variable<QueuePosition, QueuePosition>> get_variable, out GCodeVariable<QueuePosition> gcode_variable)
        {
            return DeclareGlobalVariable(OSAI_VARCODE.E_CODE, address, get_variable, out gcode_variable);
        }
        public GCodeString DeclareGlobalVariable<T>(OSAI_VARCODE varcode, Union<uint, (uint index, uint lenght)> address, IOSAI_AddressVariable address_variable, out GCodeVariable<T> gcode_variable)
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

        // ARRAY
        public override GCodeString DeclareGlobalArray<T>(string[] array_name, out GCodeArray<T> array)
        {
            throw new NotImplementedException();
        }
        public override GCodeString DeclareLocalArray<T>(string[] array_name, Union<GCodeVariable<T>, T> value, out GCodeArray<T> gcode_array)
        {
            gcode_array = new GCodeArray<T>()
            {
                Variables = array_name.Select(n =>
                {
                    DeclareLocalVariable(n, value, out var variable);
                    return variable;
                }).ToArray(),
            };
            return gcode_array.Foreach((v, i) => v.Declare);
        }
        public GCodeString DeclareGlobalArray(uint address, Func<OSAI_VariableStore, IFLUX_Array<string, string>> get_variable, out GCodeArray<string> gcode_variable)
        {
            return DeclareGlobalArray(OSAI_VARCODE.LS_CODE, address, get_variable, out gcode_variable);
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

        // MACRO
        public override GCodeString ExecuteParamacro(GCodeVariable<string> path)
        {
            return $"(CLS, ?{path})";
        }
        public override GCodeString ExecuteParamacro(string folder, string name)
        {
            return $"(CLS, {Connection.CombinePaths(folder, name)})";
        }

        // STRING
        public GCodeString ReadDateTime(out GCodeVariable<string> date_time)
        {
            DeclareLocalVariable((0, 22), "", out date_time);
            return GCodeString.Create(
                $"(GDT, D2, T0, SC0.10, SC11.11)",
                "SC10.1 = \";\"");
        }
        public GCodeString ToString<T>(GCodeVariable<T> variable, uint address, out GCodeVariable<string> str_variable)
        {
            str_variable = default;
            if (typeof(T) == typeof(string))
                throw new ArgumentException();

            DeclareLocalVariable(address, "", out str_variable);
            return $"(N2S, \"%d\", {variable}, {str_variable})";
        }
        public GCodeString AppendString(GCodeVariable<string> var, params Union<GCodeString, GCodeVariable<string>>[] strings)
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
    }
}
