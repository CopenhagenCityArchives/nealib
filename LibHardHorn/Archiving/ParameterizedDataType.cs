using HardHorn.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HardHorn.Archiving
{
    public class ParameterizedDataType : NotifyPropertyChangedBase, IComparable
    {
        static Regex regex = new Regex(@"^(?<datatype>[a-zA-Z]+( *[a-zA-Z]+)*) *(\((?<params>\d+(,\d+)*)\))?$");

        DataType _dataType;
        public DataType DataType
        {
            get
            {
                return _dataType;
            }
            set
            {
                _dataType = value;
                NotifyOfPropertyChanged("DataType");
            }
        }

        Parameter _parameter;
        public Parameter Parameter
        {
            get
            {
                return _parameter;
            }
            set
            {
                _parameter = value;
                if (value != null)
                {
                    _parameter.CollectionChanged += (s, a) =>
                    {
                        NotifyOfPropertyChanged("ParameterString");
                        NotifyOfPropertyChanged("Parameter");
                    };
                    foreach (var pItem in value)
                    {
                        pItem.PropertyChanged += (s, a) =>
                        {
                            NotifyOfPropertyChanged("ParameterString");
                            NotifyOfPropertyChanged("Parameter");
                        };
                    }
                }
                NotifyOfPropertyChanged("ParameterString");
                NotifyOfPropertyChanged("Parameter");
            }
        }

        public string ParameterString { get { return Parameter == null ? "" : Parameter.ToString(); } }

        public void AddParameterItem(int i)
        {
            var item = new ParameterItem(i);
            item.PropertyChanged += (s, a) => {
                NotifyOfPropertyChanged("ParameterString");
                NotifyOfPropertyChanged("Parameter");
            };
            Parameter.Add(item);
        }

        public void RemoveParameterItem(int i)
        {
            if (i >= 0 && i < Parameter.Count)
            {
                Parameter.RemoveAt(i);
            }
        }

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

        public static ParameterizedDataType Parse(XElement element, Table table, Column column)
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
                    throw new ArchiveVersionColumnTypeParsingException("Could not parse the datatype.", element.Value, element, column, table);
                }

                var parameterGroup = match.Groups["params"];
                Parameter parameter = null;
                if (parameterGroup.Success)
                {
                    var parameters = new List<string>(parameterGroup.Value.Split(',')).Select(n => int.Parse(n));

                    parameter = new Parameter(parameters.ToArray());
                }

                if (!DataTypeUtility.ValidateParameterLength(dataType, parameter))
                {
                    throw new ArchiveVersionColumnTypeParsingException("Could not parse the datatype.", element.Value, element, column, table);
                }

                return new ParameterizedDataType(dataType, parameter, element.Value);
            }
            else
            {
                throw new ArchiveVersionColumnTypeParsingException("Could not parse the datatype.", element.Value, element, column, table);
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

            if (Parameter != null && Parameter.Count > 0)
            {
                repr += Parameter.ToString();
            }

            return repr;
        }
    }
}
