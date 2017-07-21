﻿using System;
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
        ArchiveVersion AV;

        dynamic VerifyAV1;
        dynamic VerifyAV2;
        dynamic VerifyAV3;
        dynamic VerifyAV4;

        string VerifyAV1JSON;
        string VerifyAV2JSON;
        string VerifyAV3JSON;
        string VerifyAV4JSON;

        public ArchiveVersionTest()
        {
            var testTables = new List<Table>();
            var table1 = new Table("TABLE1", "table1", 10, "A table.", new List<Column>());
            table1.Columns.Add(new Column(table1, "COLUMN1", DataType.INTEGER, false, null, "A column.", "c1"));
            table1.Columns.Add(new Column(table1, "COLUMN2", DataType.DECIMAL, true, new int[] { 5, 10 }, "A column.", "c2"));
            var table2 = new Table("TABLE2", "table2", 5, "Another table.", new List<Column>());
            table2.Columns.Add(new Column(table2, "COLUMN1", DataType.INTEGER, false, null, "A column", "c1"));
            testTables.Add(table1);
            testTables.Add(table2);
            AV = new ArchiveVersion("avid.test.10", "path", testTables);

            dynamic vTable1Keep = new ExpandoObject();
            vTable1Keep.name = "TABLE1";
            vTable1Keep.keep = true;
            dynamic vTable2Keep = new ExpandoObject();
            vTable2Keep.name = "TABLE2";
            vTable2Keep.keep = true;
            dynamic vTable2NoKeep = new ExpandoObject();
            vTable2NoKeep.name = "TABLE2";
            vTable2NoKeep.keep = false;
            dynamic vTable3Keep = new ExpandoObject();
            vTable3Keep.name = "TABLE3";
            vTable3Keep.keep = true;

            VerifyAV1 = new ExpandoObject();
            VerifyAV1.tableIndex = new List<dynamic>(new dynamic[] { vTable1Keep, vTable2Keep });
            VerifyAV2 = new ExpandoObject();
            VerifyAV2.tableIndex = new List<dynamic>(new dynamic[] { vTable1Keep, vTable2Keep, vTable3Keep });
            VerifyAV3 = new ExpandoObject();
            VerifyAV3.tableIndex = new List<dynamic>(new dynamic[] { vTable1Keep, vTable2NoKeep });
            VerifyAV4 = new ExpandoObject();
            VerifyAV4.tableIndex = new List<dynamic>(new dynamic[] { vTable1Keep });

            VerifyAV1JSON = "{ \"tableIndex\": [ { \"name\":\"table1\", \"keep\":\"true\" }, { \"name\":\"table2\", \"keep\":\"true\" } ] }";
            VerifyAV2JSON = "{ \"tableIndex\": [ { \"name\":\"table1\", \"keep\":\"true\" }, { \"name\":\"table2\", \"keep\":\"true\" }, { \"name\":\"table3\", \"keep\":\"true\" } ] }";
            VerifyAV3JSON = "{ \"tableIndex\": [ { \"name\":\"table1\", \"keep\":\"true\" }, { \"name\":\"table2\", \"keep\":\"false\" } ] }";
            VerifyAV4JSON = "{ \"tableIndex\": [ { \"name\":\"table1\", \"keep\":\"true\" } ] }";
        }

        [TestMethod]
        public void TestVerifyPositive()
        {
            IEnumerable<ArchiveVersionVerificationError> errorList = AV.Verify(VerifyAV1);
            Assert.AreEqual(0, errorList.Count());
        }

        [TestMethod]
        public void TestVerifyTableNotKept()
        {
            IEnumerable<ArchiveVersionVerificationError> errorList = AV.Verify(VerifyAV2);
            Assert.AreEqual(1, errorList.Count());
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.TableNotKept, errorList.First().Type);
        }

        [TestMethod]
        public void TestVerifyTableKeptInError()
        {
            IEnumerable<ArchiveVersionVerificationError> errorList = AV.Verify(VerifyAV3);
            Assert.AreEqual(1, errorList.Count());
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.TableKeptInError, errorList.First().Type);
        }

        [TestMethod]
        public void TestVerifyTableUnknown()
        {
            IEnumerable<ArchiveVersionVerificationError> errorList = AV.Verify(VerifyAV4);
            Assert.AreEqual(1, errorList.Count());
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.UnknownTable, errorList.First().Type);
        }


        [TestMethod]
        public void TestVerifyJSONPositive()
        {
            IEnumerable<ArchiveVersionVerificationError> errorList = AV.VerifyJSON(VerifyAV1JSON);
            Assert.AreEqual(0, errorList.Count());
        }

        [TestMethod]
        public void TestVerifyJSONTableNotKept()
        {
            IEnumerable<ArchiveVersionVerificationError> errorList = AV.VerifyJSON(VerifyAV2JSON);
            Assert.AreEqual(1, errorList.Count());
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.TableNotKept, errorList.First().Type);
        }

        [TestMethod]
        public void TestVerifyJSONTableKeptInError()
        {
            IEnumerable<ArchiveVersionVerificationError> errorList = AV.VerifyJSON(VerifyAV3JSON);
            Assert.AreEqual(1, errorList.Count());
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.TableKeptInError, errorList.First().Type);
        }

        [TestMethod]
        public void TestVerifyJSONTableUnknown()
        {
            IEnumerable<ArchiveVersionVerificationError> errorList = AV.VerifyJSON(VerifyAV4JSON);
            Assert.AreEqual(1, errorList.Count());
            Assert.AreEqual(ArchiveVersionVerificationError.ErrorType.UnknownTable, errorList.First().Type);
        }
    }
}