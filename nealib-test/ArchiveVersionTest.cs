using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

using NEA.Archiving;
using NEA.Analysis;
using NEA.Testing.Utilities;

namespace NEA.Testing
{
    [TestClass]
    public class ArchiveVersionTest
    {
        ArchiveVersion AV;

        string VerifyAV1JSON;
        string VerifyAV2JSON;
        string VerifyAV3JSON;
        string VerifyAV4JSON;

        public ArchiveVersionTest()
        {
            var testTables = new List<Table>();
            var table1 = new Table("TABLE1", "table1", 10, "A table.", new List<Column>(), new PrimaryKey("PK1", new List<string>(new string[] { "COLUMN1" })), new List<ForeignKey>());
            table1.Columns.Add(new Column(table1, "COLUMN1", new ParameterizedDataType(DataType.INTEGER, null), "INT", false, "A column.", "c1", 1, null, null));
            table1.Columns.Add(new Column(table1, "COLUMN2", new ParameterizedDataType(DataType.DECIMAL, Parameter.WithPrecisionAndScale(5, 10)), "DEC(5,10)", true, "A column.", "c2", 2, null, null));
            var table2 = new Table("TABLE2", "table2", 5, "Another table.", new List<Column>(), new PrimaryKey("PK2", new List<string>(new string[] { "COLUMN1" })), new List<ForeignKey>());
            table2.Columns.Add(new Column(table2, "COLUMN1", new ParameterizedDataType(DataType.INTEGER, null), "INT", false, "A column", "c1", 1, null, null));
            testTables.Add(table1);
            testTables.Add(table2);
            AV = new ArchiveVersion("avid.test.10", "path", testTables);

            VerifyAV1JSON = "{ \"tableIndex\": [ { \"name\":\"table1\", \"keep\":true }, { \"name\":\"table2\", \"keep\":true } ] }";
            VerifyAV2JSON = "{ \"tableIndex\": [ { \"name\":\"table1\", \"keep\":true }, { \"name\":\"table2\", \"keep\":true }, { \"name\":\"table3\", \"keep\":true } ] }";
            VerifyAV3JSON = "{ \"tableIndex\": [ { \"name\":\"table1\", \"keep\":true }, { \"name\":\"table2\", \"keep\":false } ] }";
            VerifyAV4JSON = "{ \"tableIndex\": [ { \"name\":\"table1\", \"keep\":true } ] }";
        }

        [TestMethod]
        public void TestVerifyJSONPositive()
        {
            var errorList = AV.VerifyJSON(VerifyAV1JSON).ToList();
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.Value, errorList[0].Type);
            Assert.AreEqual("archiveIndex", errorList[0].Message);
            Assert.AreEqual(1, errorList.Count);
        }

        [TestMethod]
        public void TestVerifyJSONTableNotKept()
        {
            var errorList = AV.VerifyJSON(VerifyAV2JSON).ToList();
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.Value, errorList[0].Type);
            Assert.AreEqual("archiveIndex", errorList[0].Message);
            Assert.AreEqual(2, errorList.Count); // Expect 1 extra error for missing archiveIndex
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.TableNotKept, errorList[1].Type);
        }

        [TestMethod]
        public void TestVerifyJSONTableKeptInError()
        {
            var errorList = AV.VerifyJSON(VerifyAV3JSON).ToList();
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.Value, errorList[0].Type);
            Assert.AreEqual("archiveIndex", errorList[0].Message);
            Assert.AreEqual(2, errorList.Count); // Expect 1 extra error for missing archiveIndex
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.TableKeptInError, errorList[1].Type);
        }

        [TestMethod]
        public void TestVerifyJSONTableUnknown()
        {
            var errorList = AV.VerifyJSON(VerifyAV4JSON).ToList();
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.Value, errorList[0].Type);
            Assert.AreEqual("archiveIndex", errorList[0].Message);
            Assert.AreEqual(2, errorList.Count); // Expect 1 extra error for missing archiveIndex
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.UnknownTable, errorList[1].Type);
        }

        [TestMethod]
        [DeploymentItem(@"..\..\TestResources", @"TestResources")]
        public void TestLoadTableIndex()
        {
            var AV = ArchiveVersion.Load(@"TestResources\AVID.TEST.1.1", new TestLogger());
            var tables = AV.Tables.ToList();

            Assert.AreEqual(2, tables.Count);

            Assert.AreEqual("PERSONER", tables[0].Name);
            Assert.AreEqual("table1", tables[0].Folder);
            Assert.AreEqual("Tabel over personer.", tables[0].Description);

            Assert.AreEqual("ID", tables[0].Columns[0].Name);
            Assert.AreEqual("NAVN", tables[0].Columns[1].Name);
            Assert.AreEqual("TLFNR", tables[0].Columns[2].Name);

            Assert.AreEqual("ADDRESSER", tables[1].Name);
            Assert.AreEqual("table2", tables[1].Folder);
            Assert.AreEqual("Personaddresser.", tables[1].Description);

            Assert.AreEqual("PERSON_ID", tables[1].Columns[0].Name);
            Assert.AreEqual("ADDRESSE", tables[1].Columns[1].Name);
            Assert.AreEqual("INDFLYT", tables[1].Columns[2].Name);
        }

        [TestMethod]
        public void TestParameterCompareTo()
        {
            Assert.AreEqual(-1, Parameter.WithPrecision(1).CompareTo(Parameter.WithPrecision(2)));
            Assert.AreEqual(1, Parameter.WithPrecision(2).CompareTo(Parameter.WithPrecision(1)));

            Assert.AreEqual(-1, Parameter.WithLength(1).CompareTo(Parameter.WithLength(2)));
            Assert.AreEqual(1, Parameter.WithLength(2).CompareTo(Parameter.WithLength(1)));


            // Compare same Parameter
            for (uint i = 0; i < 10; i++)
            {
                var length1 = Parameter.WithLength(i);
                var length2 = Parameter.WithLength(i);
                Assert.AreEqual(0, length1.CompareTo(length1));
                Assert.AreEqual(0, length2.CompareTo(length1));
                Assert.AreEqual(0, length1.CompareTo(length2));
                Assert.AreEqual(0, length2.CompareTo(length2));

                var prec1 = Parameter.WithPrecision(i);
                var prec2 = Parameter.WithPrecision(i);
                Assert.AreEqual(0, prec1.CompareTo(prec1));
                Assert.AreEqual(0, prec2.CompareTo(prec1));
                Assert.AreEqual(0, prec1.CompareTo(prec2));
                Assert.AreEqual(0, prec2.CompareTo(prec2));
            }

            for (uint i = 0; i < 10; i++)
            {
                for (uint j = 0; j < 10; j++)
                {
                    // same precision, possible different scale
                    var parami = Parameter.WithPrecisionAndScale(10, i);
                    var paramj = Parameter.WithPrecisionAndScale(10, j);
                    Assert.AreEqual(i.CompareTo(j), parami.CompareTo(paramj));
                    Assert.AreEqual(j.CompareTo(i), paramj.CompareTo(parami));

                    // compare with different first parameter items
                    var param1 = Parameter.WithPrecisionAndScale(1, i);
                    var param2 = Parameter.WithPrecisionAndScale(2, j);
                    Assert.AreEqual(-1, param1.CompareTo(param2));
                    Assert.AreEqual(1, param2.CompareTo(param1));
                }
            }
        }
    }
}
