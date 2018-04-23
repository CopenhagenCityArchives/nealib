using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HardHorn.Archiving
{
    public class Post
    {
        public int? Line { get; set; }
        public int? Position { get; set; }
        public bool IsNull { get; set; }
        public string Data { get; set; }

        public Post()
        { }

        public Post(string data, bool isNull, int? line = null, int? position = null)
        {
            Line = line;
            Position = position;
            IsNull = isNull;
            Data = data;
        }

        public override string ToString()
        {
            return Data;
        }

        public Post ReplacePattern(Regex pattern, string replacement)
        {
            if (IsNull)
            {
                return null;
            }
            else
            {
                return new Post(pattern.Replace(Data, replacement), IsNull, Line, Position);
            }
        }
    }
}
