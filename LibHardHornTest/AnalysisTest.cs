using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HardHorn.Archiving;
using System.Collections.Generic;
using HardHorn.Analysis;
using LibHardHornTest.Utilities;

namespace LibHardHornTest
{
    [TestClass]
    public class AnalysisTest
    {
        [TestMethod]
        public void TestAnalyzeLengths()
        {
            var av = new ArchiveVersion("AVID.TEST.1", "E:\\AVID.TEST.1",
                new Table[] {
                    new Table(null, "TABLE1", "table1", 10, "", new List<Column>(new Column[] {
                        new Column(null, "ID", DataType.INTEGER, false, new int[] { }, "", "c1"),
                        new Column(null, "DATE", DataType.DATE, false, new int[] { }, "", "c2"),
                        new Column(null, "NAME", DataType.CHARACTER_VARYING, false, new int[] { }, "", "c3"),
                        new Column(null, "UUID", DataType.CHARACTER, false, new int[] { }, "", "c4")
                    })),
                    new Table(null, "TABLE2", "table2", 10, "", new List<Column>(new Column[] {
                        new Column(null, "FOREIGN_ID", DataType.INTEGER, false, new int[] { }, "", "c1"),
                        new Column(null, "DATA", DataType.CHARACTER_VARYING, false, new int[] { }, "", "c2"),
                        new Column(null, "TIMECOL", DataType.TIMESTAMP, false, new int[] { }, "", "c3")
                    }))
                });

            var analyzer = new Analyzer(av, new TestLogger());
        }

        [TestMethod]
        public void TestAnalyzePost()
        {
        }
    }
}
