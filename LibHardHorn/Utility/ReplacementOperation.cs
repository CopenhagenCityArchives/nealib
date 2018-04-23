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
        public Column Column { get; set; }
        public Regex Pattern { get; set; }
        public string Replacement { get; set; }

        public ReplacementOperation(Column column, Regex pattern, string replacement)
        {
            Column = column;
            Pattern = pattern;
            Replacement = replacement;
        }
    }
}
