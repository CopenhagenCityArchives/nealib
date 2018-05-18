using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HardHorn.Archiving;

namespace HardHorn.Utility
{
    public class ReplacementOperation
    {
        public Table Table { get; set; }
        public Column Column { get; set; }
        public Regex Pattern { get; set; }
        public string Replacement { get; set; }

        public ReplacementOperation(Table table, Column column, Regex pattern, string replacement)
        {
            Table = table;
            Column = column;
            Pattern = pattern;
            Replacement = replacement;
        }
    }
}
