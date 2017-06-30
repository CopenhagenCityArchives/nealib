using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HardHorn.ArchiveVersion;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace LibHardHornTest
{
    [TestClass]
    public class ArchiveVersionTest
    {
        [TestMethod]
        public void TestVerify()
        {
            var testTables = new List<Table>();
            var table1 = new Table("TABLE1", "table1", 10, "A table.", new List<Column>());
            table1.Columns.Add(new Column(table1, "COLUMN1", DataType.INTEGER, false, null, "A column.", "c1"));
            table1.Columns.Add(new Column(table1, "COLUMN2", DataType.DECIMAL, true, new int[] { 5, 10 }, "A column.", "c2"));
            var table2 = new Table("TABLE2", "table2", 5, "Another table.", new List<Column>());
            table2.Columns.Add(new Column(table2, "COLUMN1", DataType.INTEGER, false, null, "A column", "c1"));
            testTables.Add(table1);
            testTables.Add(table2);
            var AV = new ArchiveVersion("avid.test.10", "path", testTables);

            dynamic verifyTable1 = new ExpandoObject();
            verifyTable1.name = "TABLE1";
            verifyTable1.keep = true;
            dynamic verifyTable2 = new ExpandoObject();
            verifyTable2.name = "TABLE2";
            verifyTable2.keep = true;
            dynamic verifyTable3 = new ExpandoObject();
            verifyTable3.name = "TABLE3";
            verifyTable3.keep = true;
            var verifyTableIndex = new List<dynamic>();
            verifyTableIndex.Add(verifyTable1);
            verifyTableIndex.Add(verifyTable2);
            verifyTableIndex.Add(verifyTable3);

            dynamic verifyAV = new ExpandoObject();
            verifyAV.tableIndex = verifyTableIndex;

            IEnumerable<ArchiveVersionVerificationError> errorList = AV.Verify(verifyAV);
            Assert.AreEqual(1, errorList.Count());
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.TableNotKept, errorList.First().Type);
        }
    }
}
