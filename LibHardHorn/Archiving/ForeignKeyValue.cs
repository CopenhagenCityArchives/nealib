using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Archiving
{
    public class ForeignKeyValue : IEquatable<ForeignKeyValue>
    {
        public int Count { get; private set; }
        public string[] Values { get; private set; }

        public ForeignKeyValue(params string[] values)
        {
            Count = values.Length;
            Values = values;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 19;
                foreach (var value in Values)
                {
                    hash *= 31 + value == null ? 0 : value.GetHashCode();
                }
                return hash;
            }
        }

        public bool Equals(ForeignKeyValue other)
        {
            if (Count != other.Count)
                return false;
            for (int i = 0; i < Count; i++)
                if (Values[i] != other.Values[i])
                    return false;
            return true;
        }

        public override string ToString()
        {
            return string.Join("/", Values);
        }
    }
}
