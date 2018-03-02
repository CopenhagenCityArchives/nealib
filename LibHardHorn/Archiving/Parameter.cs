using HardHorn.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Archiving
{
    /// <summary>
    /// A parameter list.
    /// </summary>
    public class Parameter : ObservableCollection<ParameterItem>, IComparable
    {
        /// <summary>
        /// Get the string representation.
        /// </summary>
        /// <returns>The string representation of the parameter list.</returns>
        public override string ToString()
        {
            if (Count > 0)
            {
                return "(" + string.Join(", ", this.Select(p => p.Value)) + ")";
            }
            else
            {
                return "";
            }
        }

        public string ToString(DataType dataType)
        {
            if (Count == 0)
            {
                return string.Empty;
            }

            var repr = string.Empty;
            for (int i = 0; i < Count; i++)
            {
                if (i == 0 && (dataType == DataType.TIMESTAMP || dataType == DataType.TIMESTAMP_WITH_TIME_ZONE) && this[i].Value == 6)
                {
                    continue;
                }
                else if (i == 0 && (dataType == DataType.TIME || dataType == DataType.TIME_WITH_TIME_ZONE) && this[i].Value == 0)
                {
                    continue;
                }
                else if (i == 0 && (dataType == DataType.CHARACTER || dataType == DataType.CHARACTER_VARYING || dataType == DataType.NATIONAL_CHARACTER_VARYING || dataType == DataType.NATIONAL_CHARACTER) && this[i].Value == 1)
                {
                    continue;
                }
                else if (i == 1 && (dataType == DataType.NUMERIC || dataType == DataType.DECIMAL) && this[i].Value == 0)
                {
                    continue;
                }
                else if (i > 0)
                {
                    repr += ", " + this[i].Value;
                }
                else
                {
                    repr += this[i].Value;
                }
            }
            return string.IsNullOrEmpty(repr) ? null : string.Format("({0})", repr);
        }

        public int CompareTo(object obj)
        {
            var other = obj as Parameter;
            if (other != null)
            {
                if (Count == other.Count)
                {
                    for (int i = 0; i < Count; ++i)
                    {
                        var comp = this[i].CompareTo(other[i]);
                        if (comp != 0)
                        {
                            return comp;
                        }
                    }
                    return 0;
                }
                else if (Count > other.Count)
                {
                    for (int i = 0; i < other.Count; ++i)
                    {
                        var comp = this[i].CompareTo(other[i]);
                        if (comp != 0)
                        {
                            return comp;
                        }
                    }
                    return 1;
                }
                else // Length < other.Length
                {
                    for (int i = 0; i < Count; ++i)
                    {
                        var comp = this[i].CompareTo(other[i]);
                        if (comp != 0)
                        {
                            return comp;
                        }
                    }
                    return -1;
                }
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// Construct a parameter list.
        /// </summary>
        /// <param name="param">The integer parameters.</param>
        public Parameter(params int[] param) : base(param.Select(i => new ParameterItem(i))) { }

        /// <summary>
        /// Construct a parameter list.
        /// </summary>
        /// <param name="param">The integer parameters.</param>
        public Parameter(DataType dataType, params int[] param)
        {
            if (!DataTypeUtility.ValidateParameters(dataType, param))
            {
                throw new InvalidOperationException();
            }

            switch (dataType)
            {
                case DataType.CHARACTER:
                case DataType.CHARACTER_VARYING:
                case DataType.NATIONAL_CHARACTER:
                case DataType.NATIONAL_CHARACTER_VARYING:
                    if (param == null || param.Length == 0)
                    {
                        Add(new ParameterItem(1));
                    }
                    else
                    {
                        foreach (var p in param) Add(new ParameterItem(p));
                    }
                    break;
                case DataType.TIME:
                case DataType.TIME_WITH_TIME_ZONE:
                    if (param == null || param.Length == 0)
                    {
                        Add(new ParameterItem(0));
                    }
                    else
                    {
                        foreach (var p in param) Add(new ParameterItem(p));
                    }
                    break;
                case DataType.TIMESTAMP:
                case DataType.TIMESTAMP_WITH_TIME_ZONE:
                    if (param == null || param.Length == 0)
                    {
                        Add(new ParameterItem(6));
                    }
                    else
                    {
                        foreach (var p in param) Add(new ParameterItem(p));
                    }
                    break;
                case DataType.DECIMAL:
                case DataType.NUMERIC:
                    if (param.Length == 1)
                    {
                        Add(new ParameterItem(param[0]));
                        Add(new ParameterItem(0));
                    }
                    else
                    {
                        foreach (var p in param) Add(new ParameterItem(p));
                    }
                    break;
                default:
                    if (param != null)
                        foreach (var p in param)
                            Add(new ParameterItem(p));
                    break;
            }
        }
    }
}
