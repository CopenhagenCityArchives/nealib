﻿using System;
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
                        new Column(null, "ID", DataType.INTEGER, false, new int[] { }, "", "c1", 1),
                        new Column(null, "DATE", DataType.DATE, false, new int[] { }, "", "c2", 2),
                        new Column(null, "NAME", DataType.CHARACTER_VARYING, false, new int[] { }, "", "c3", 3),
                        new Column(null, "UUID", DataType.CHARACTER, false, new int[] { }, "", "c4", 4)
                    })),
                    new Table(null, "TABLE2", "table2", 10, "", new List<Column>(new Column[] {
                        new Column(null, "FOREIGN_ID", DataType.INTEGER, false, new int[] { }, "", "c1", 1),
                        new Column(null, "DATA", DataType.CHARACTER_VARYING, false, new int[] { }, "", "c2", 2),
                        new Column(null, "TIMECOL", DataType.TIMESTAMP, false, new int[] { }, "", "c3", 3)
                    }))
                });

            var analyzer = new Analyzer(av, new TestLogger());
        }

        public void AssertOkay(Column column, Test test, string data, int line = 0, int pos = 0, bool isNull = false)
        {
            var result = test.GetResult(new Post(data, line, pos, isNull), column);
            Assert.AreEqual(Test.Result.OKAY, result);
        }

        public void AssertError(Column column, Test test, string data)
        {
            var result = test.GetResult(new Post(data, 0, 0, false), column);
            Assert.AreEqual(Test.Result.ERROR, result);
        }

        [TestMethod]
        public void TestAnalyzeDateFormatTest()
        {
            var test = Test.DateFormatTest();
            var column = new Column(null, "name", DataType.DATE, false, new int[0], "desc", "c1", 1);

            AssertOkay(column, test, "0001-01-01");
            AssertOkay(column, test, "2016-05-23");
            AssertOkay(column, test, "2016-05-23");
            AssertOkay(column, test, "2016-05-23");
            AssertOkay(column, test, "1994-12-31");
            AssertOkay(column, test, "2016-12-01");
            AssertOkay(column, test, "2017-01-31");
            AssertOkay(column, test, "1991-05-16");
            AssertOkay(column, test, "9999-12-31");

            AssertError(column, test, "99-1-31"); // two-digit year
            AssertError(column, test, "2017-12-1"); // one-digit-month
            AssertError(column, test, "2017-1-31"); // one-digit day
            AssertError(column, test, "abc"); // letters
            AssertError(column, test, "2016-15-23"); // Month out of range
            AssertError(column, test, "2016-06-80"); // Day out of range
            AssertError(column, test, "1905-10"); // Day missing
            AssertError(column, test, ""); // Empty string
            AssertError(column, test, "   "); // Blank string
        }

        [TestMethod]
        public void TestAnalyzeTimeFormatTest()
        {
            var test = Test.TimeFormatTest();
            var column = new Column(null, "name", DataType.TIME, false, new int[0], "desc", "c1", 1);

            AssertOkay(column, test, "14:15:20");
            AssertOkay(column, test, "00:00:00"); // minimum
            AssertOkay(column, test, "23:59:59"); // maximum

            AssertError(column, test, "14:15:20 "); // with a blank
            AssertError(column, test, " 14:15:20"); // with a blank
            AssertError(column, test, "25:20:11"); // Hour out of range
            AssertError(column, test, "1:11:11"); // 1-digit hour
            AssertError(column, test, "111:11:11"); // 3-digit hour
            AssertError(column, test, "10:63:30"); // minute out of range
            AssertError(column, test, "10:5:30"); // 1-digit minute
            AssertError(column, test, "10:555:30"); // 3-digit minute
            AssertError(column, test, "10:10:99"); // second out of range
            AssertError(column, test, "10:10:1"); // 1-digit second
            AssertError(column, test, "10:10:123"); // 3-digit second
            AssertError(column, test, ""); // empty string
        }

        [TestMethod]
        public void TestAnalyzeTimeWithTimezoneFormatTest()
        {
            var test = Test.TimeWithTimeZoneTest();
            var column = new Column(null, "name", DataType.TIME_WITH_TIME_ZONE, false, new int[0], "desc", "c1", 1);

            AssertOkay(column, test, "14:15:20Z");
            AssertOkay(column, test, "14:15:20+12:00");
            AssertOkay(column, test, "14:15:20-12:00");
            AssertOkay(column, test, "13:37:00+05:30");
            AssertOkay(column, test, "00:00:00Z"); // minimum
            AssertOkay(column, test, "23:59:59Z"); // maximum
            AssertOkay(column, test, "00:00:00+00:00"); // minimum
            AssertOkay(column, test, "23:59:59+12:00"); // maximum
            AssertOkay(column, test, "23:59:59-00:00"); // minimum
            AssertOkay(column, test, "23:59:59-12:00"); // maximum

            AssertError(column, test, "14:15:20+5:00"); // 1 digit time zone hour
            AssertError(column, test, "14:15:20+005:00"); // 3 digit time zone hour
            AssertError(column, test, "14:15:20+05:0"); // 1 digit time zone minute
            AssertError(column, test, "14:15:20+05:000"); // 3 digit time zone minute
            AssertError(column, test, "14:15:20+13:00"); // time zone hour out of range
            AssertError(column, test, "14:15:20-13:00"); // time zone hour out of range
            AssertError(column, test, "14:15:20+01:60"); // time zone minute out of range
            AssertError(column, test, "14:15:20"); // missing time zone
            AssertError(column, test, "14:15:20+01:00 "); // with a blank
            AssertError(column, test, " 14:15:20+01:00"); // with a blank
            AssertError(column, test, "25:20:11+01:00"); // Hour out of range
            AssertError(column, test, "1:11:11+01:00"); // 1-digit hour
            AssertError(column, test, "111:11:11+01:00"); // 3-digit hour
            AssertError(column, test, "10:63:30+01:00"); // minute out of range
            AssertError(column, test, "10:5:30+01:00"); // 1-digit minute
            AssertError(column, test, "10:555:30+01:00"); // 3-digit minute
            AssertError(column, test, "10:10:99+01:00"); // second out of range
            AssertError(column, test, "10:10:1+01:00"); // 1-digit second
            AssertError(column, test, "10:10:123+01:00"); // 3-digit second
            AssertError(column, test, "abc"); // non-format string
            AssertError(column, test, ""); // empty string
        }

        [TestMethod]
        public void TestAnalyzeTimestampWithTimezoneFormatTest()
        {
            var test = Test.TimestampWithTimeZoneFormatTest();
            var column = new Column(null, "name", DataType.TIMESTAMP_WITH_TIME_ZONE, false, new int[0], "desc", "c1", 1);

            AssertOkay(column, test, "2000-08-10T14:15:20Z");
            AssertOkay(column, test, "2000-08-10T14:15:20+12:00");
            AssertOkay(column, test, "2000-08-10T14:15:20-12:00");
            AssertOkay(column, test, "2000-08-10T13:37:00+05:30");
            AssertOkay(column, test, "2000-08-10T00:00:00Z"); // minimum
            AssertOkay(column, test, "2000-08-10T23:59:59Z"); // maximum
            AssertOkay(column, test, "2000-08-10T00:00:00+00:00"); // minimum
            AssertOkay(column, test, "2000-08-10T23:59:59+12:00"); // maximum
            AssertOkay(column, test, "2000-08-10T23:59:59-00:00"); // minimum
            AssertOkay(column, test, "2000-08-10T23:59:59-12:00"); // maximum

            AssertError(column, test, "2000-08-10T14:15:20+5:00"); // 1 digit time zone hour
            AssertError(column, test, "2000-08-10T14:15:20+005:00"); // 3 digit time zone hour
            AssertError(column, test, "2000-08-10T14:15:20+05:0"); // 1 digit time zone minute
            AssertError(column, test, "2000-08-10T14:15:20+05:000"); // 3 digit time zone minute
            AssertError(column, test, "2000-08-10T14:15:20+13:00"); // time zone hour out of range
            AssertError(column, test, "2000-08-10T14:15:20-13:00"); // time zone hour out of range
            AssertError(column, test, "2000-08-10T14:15:20+01:60"); // time zone minute out of range
            AssertError(column, test, "2000-08-10T14:15:20"); // missing time zone
            AssertError(column, test, "2000-08-10T14:15:20+01:00 "); // with a blank
            AssertError(column, test, " 2000-08-10T14:15:20+01:00"); // with a blank
            AssertError(column, test, "2000-08-10T25:20:11+01:00"); // Hour out of range
            AssertError(column, test, "2000-08-10T1:11:11+01:00"); // 1-digit hour
            AssertError(column, test, "2000-08-10T111:11:11+01:00"); // 3-digit hour
            AssertError(column, test, "2000-08-10T10:63:30+01:00"); // minute out of range
            AssertError(column, test, "2000-08-10T10:5:30+01:00"); // 1-digit minute
            AssertError(column, test, "2000-08-10T10:555:30+01:00"); // 3-digit minute
            AssertError(column, test, "2000-08-10T10:10:99+01:00"); // second out of range
            AssertError(column, test, "2000-08-10T10:10:1+01:00"); // 1-digit second
            AssertError(column, test, "2000-08-10T10:10:123+01:00"); // 3-digit second
            AssertError(column, test, "abc"); // non-format string
            AssertError(column, test, ""); // empty string
        }
    }
}
