using HardHorn.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HardHorn.Archiving
{
    public class ParameterizedDataType : IComparable
    {
        static Regex regex = new Regex(@"^(?<datatype>[a-zA-Z]+( *[a-zA-Z]+)*) *(\((?<params>\d+(,\d+)*)\))?$");
        public DataType DataType { get; private set; }
        public Parameter Parameter { get; private set; }
        public string Parsed { get; private set; }

        public ParameterizedDataType(DataType dataType, Parameter parameter, string parsed = null)
        {
            DataType = dataType;
            Parameter = parameter;
            Parsed = parsed;
        }

        public static ParameterizedDataType GetUndefined()
        {
            return new ParameterizedDataType(DataType.UNDEFINED, null);
        }

        public static ParameterizedDataType Parse(XElement element, Table table, string id, string name)
        {
            var match = regex.Match(element.Value);
            if (match.Success)
            {
                DataType dataType;
                try
                {
                    dataType = DataTypeUtility.Parse(match.Groups["datatype"].Value);
                }
                catch (InvalidOperationException)
                {
                    throw new ArchiveVersionColumnTypeParsingException("Could not parse the datatype.", id, name, element.Value, element, table);
                }

                var parameterGroup = match.Groups["params"];
                Parameter parameter = null;
                if (parameterGroup.Success)
                {
                    var parameters = new List<string>(parameterGroup.Value.Split(',')).Select(n => int.Parse(n));

                    if (!DataTypeUtility.ValidateParameterLength(dataType, parameters))
                    {
                        throw new ArchiveVersionColumnTypeParsingException("Could not parse the datatype.", id, name, element.Value, element, table);
                    }

                    parameter = new Parameter(parameters.ToArray());
                }
                return new ParameterizedDataType(dataType, parameter, element.Value);
            }
            else
            {
                throw new ArchiveVersionColumnTypeParsingException("Could not parse the datatype.", id, name, element.Value, element, table);
            }
        }

        public int CompareTo(object obj)
        {
            var other = obj as ParameterizedDataType;
            if (other != null)
            {
                var dataTypeComp = DataType.CompareTo(other.DataType);
                if (dataTypeComp != 0)
                {
                    return dataTypeComp;
                }
                else if (Parameter != null)
                {
                    return Parameter.CompareTo(other.Parameter);
                }
                else
                {
                    return other.Parameter == null ? 0 : -1;
                }
            }
            else
            {
                return 1;
            }
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Parsed))
            {
                return Parsed;
            }

            var repr = "";

            repr += DataTypeUtility.ToString(DataType);

            if (Parameter != null && Parameter.Length > 0)
            {
                repr += Parameter.ToString();
            }

            return repr;
        }
    }
}
