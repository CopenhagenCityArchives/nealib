using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Archiving
{
    public class Post
    {
        public int Line { get; set; }
        public int Position { get; set; }
        public bool IsNull { get; set; }
        public string Data { get; set; }

        public Post()
        { }

        public Post(string data, int line, int position, bool isNull)
        {
            Line = line;
            Position = position;
            IsNull = isNull;
            Data = data;
        }
    }
}
