using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Archiving
{
    /// <summary>
    /// A parameter list.
    /// </summary>
    public class Parameter : IComparable
    {
        int[] _param;

        public int Length { get { if (_param == null) { return 0; } else { return _param.Length; } } }

        public int this[int index]
        {
            get { return _param[index]; }
            set { _param[index] = value; }
        }

        /// <summary>
        /// Get the string representation.
        /// </summary>
        /// <returns>The string representation of the parameter list.</returns>
        public override string ToString()
        {
            if (_param == null)
            {
                return "";
            }
            else
            {
                return "(" + string.Join(", ", _param.Select(p => p.ToString())) + ")";
            }
        }

        public int CompareTo(object obj)
        {
            var other = obj as Parameter;
            if (other != null)
            {
                if (Length == other.Length)
                {
                    for (int i = 0; i < Length; ++i)
                    {
                        var comp = this[i].CompareTo(other[i]);
                        if (comp != 0)
                        {
                            return comp;
                        }
                    }
                    return 0;
                }
                else if (Length > other.Length)
                {
                    for (int i = 0; i < other.Length; ++i)
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
                    for (int i = 0; i < Length; ++i)
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
                for (int i = 0; i < Length; ++i)
                {
                    var comp = this[i].CompareTo(obj);
                    if (comp != 0)
                    {
                        return comp;
                    }
                }
                return 0;
            }
        }

        /// <summary>
        /// Construct a parameter list.
        /// </summary>
        /// <param name="param">The integer parameters.</param>
        public Parameter(params int[] param)
        {
            _param = param;
        }
    }
}
