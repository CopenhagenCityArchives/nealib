using System;
using System.Xml.Linq;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using HardHorn.Analysis;

namespace TableDataAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            string location = args[0];

            var analyzer = new DataAnalyzer(location, Console.Out);
        }
    }
}
