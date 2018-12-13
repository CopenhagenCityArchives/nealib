using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Analysis
{
    public enum AnalysisTestType
    {
        OVERFLOW,
        UNDERFLOW,
        FORMAT,
        NULL,
        BLANK,
        REGEX,
        HTML_TAG,
        ENTITY_CHAR_REF,
        REPEATING_CHAR,
        UNALLOWED_KEYWORD
    }
}
