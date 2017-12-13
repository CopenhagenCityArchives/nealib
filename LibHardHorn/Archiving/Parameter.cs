﻿using HardHorn.Utility;
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
        public Parameter(params int[] param) : base(param.Select(i => new ParameterItem(i)))
        {
        }
    }
}