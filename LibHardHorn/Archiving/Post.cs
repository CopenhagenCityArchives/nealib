﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NEA.Archiving
{
    public class Post
    {
        public int? Line { get; set; }
        public int? Position { get; set; }
        public bool IsNull { get; set; }
        public string Data { get; set; }
        public int RowIndex { get; set; }

        public Post(string data, bool isNull, int? line = null, int? position = null, int rowIndex = 0)
        {
            Line = line;
            Position = position;
            IsNull = isNull;
            Data = data;
            RowIndex = rowIndex;
        }

        public override string ToString()
        {
            return Data;
        }

        public int ReplacePattern(Regex pattern, string replacement)
        {
            int replaceCount = 0;
            if (!IsNull)
            {
                Data = pattern.Replace(Data, match => {
                    replaceCount++;
                    return match.Result(replacement);
                });
            }
            return replaceCount;
        }
    }
}
