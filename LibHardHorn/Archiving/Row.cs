using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Archiving
{
    public class Row
    {
        public int FieldCount { get; private set; }
        Post[] _fields;

        public Post this[int index]
        {
            get { return _fields[index]; }
            set { _fields[index] = value; }
        }

        public Row(int count)
        {
            FieldCount = count;
            _fields = new Post[count];
            for (int i = 0; i < count; i++)
            {
                _fields[i] = new Post();
            }
        }
    }
}
