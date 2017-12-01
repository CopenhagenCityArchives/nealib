using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Logging
{
    public enum LogLevel
    {
        NORMAL,
        ERROR,
        SUGGEST,
        SECTION
    }

    public interface ILogger
    {
        void Log(string message, LogLevel level);
    }
}
