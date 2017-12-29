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
            return "(" + string.Join(", ", this.Select(p => p.Value)) + ")";
        }

        public static Parameter GetDefaultParameter(DataType dataType)
        {
            switch (dataType)
            {
                case DataType.CHARACTER:
                case DataType.CHARACTER_VARYING:
                case DataType.NATIONAL_CHARACTER:
                case DataType.NATIONAL_CHARACTER_VARYING:
                    return new Parameter(true, 1);
                case DataType.TIME:
                    return new Parameter(true, 1);
                case DataType.TIMESTAMP:
                    return new Parameter(true, 6);
                default:
                    return null;
            }
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
        public Parameter(bool defaultValues, params int[] param) : base(param.Select(i => new ParameterItem(i, defaultValues)))
        {
        }

        public void AddDefaultParametersIfNeeded(DataType dataType)
        {
            switch (dataType)
            {
                case DataType.DECIMAL:
                    if (Count == 1)
                    {
                        Add(new ParameterItem(0, true));
                    }
                    break;
                case DataType.CHARACTER:
                case DataType.CHARACTER_VARYING:
                case DataType.NATIONAL_CHARACTER:
                case DataType.NATIONAL_CHARACTER_VARYING:
                    if (Count == 0)
                    {
                        Add(new ParameterItem(1, true));
                    }
                    break;
                default:
                    break;
            }
        }


        public bool ValidateLength(DataType type)
        {
            switch (type)
            {
                case DataType.CHARACTER:
                case DataType.CHARACTER_VARYING:
                case DataType.NATIONAL_CHARACTER:
                case DataType.NATIONAL_CHARACTER_VARYING:
                    return Count == 1;
                case DataType.TIMESTAMP:
                case DataType.TIMESTAMP_WITH_TIME_ZONE:
                case DataType.TIME:
                case DataType.TIME_WITH_TIME_ZONE:
                    return Count == 1;
                case DataType.NUMERIC:
                case DataType.DECIMAL:
                    return Count == 1 || Count == 2;
                default:
                    return Count == 0;
            }
        }
    }
}
