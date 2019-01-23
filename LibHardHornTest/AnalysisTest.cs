using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HardHorn.Archiving;
using System.Collections.Generic;
using HardHorn.Analysis;
using LibHardHornTest.Utilities;
using System.Text.RegularExpressions;
using System.Linq;
using HardHorn.Utility;

namespace LibHardHornTest
{
    [TestClass]
    public class AnalysisTest
    {
        [TestMethod]
        public void AnalyzeLengths()
        {
            var av = new ArchiveVersion("AVID.TEST.1", "E:\\AVID.TEST.1",
                new Table[] {
                    new Table("TABLE1", "table1", 10, "desc", new List<Column>(new Column[] {
                        new Column(null, "ID", new ParameterizedDataType(DataType.INTEGER, null), "INTEGER", false, "The ID.", "c1", 1, null, null),
                        new Column(null, "DATE", new ParameterizedDataType(DataType.DATE, null), "DATE", false, "", "c2", 2, null, null),
                        new Column(null, "NAME", new ParameterizedDataType(DataType.CHARACTER_VARYING, Parameter.WithLength(10)), "VARCHAR(10)", false, "", "c3", 3, null, null),
                        new Column(null, "UUID", new ParameterizedDataType(DataType.CHARACTER,  Parameter.WithLength(10)), "CHAR(10)", false, "", "c4", 4, null, null)
                    }), new PrimaryKey("PK1", new List<string>(new string[] {"ID" })), new List<ForeignKey>()),
                    new Table("TABLE2", "table2", 10, "desc", new List<Column>(new Column[] {
                        new Column(null, "FOREIGN_ID", new ParameterizedDataType(DataType.INTEGER, null), "INTEGER", false, "", "c1", 1, null, null),
                        new Column(null, "DATA", new ParameterizedDataType(DataType.CHARACTER_VARYING,  Parameter.WithLength(10)), "VARCHAR (10)", false, "", "c2", 2, null, null),
                        new Column(null, "TIMECOL", new ParameterizedDataType(DataType.TIMESTAMP, Parameter.WithPrecision(6)), "TIMESTAMP (6)", false, "", "c3", 3, null, null)
                    }), new PrimaryKey("PK2", new List<string>(new string[] {"ID" })), new List<ForeignKey>())
                });

            var analyzer = new Analyzer(av, av.Tables, new TestLogger());
        }

        public void AssertOkay(Column column, Test test, string data, bool isNull = false)
        {
            var result = test.GetResult(new Post(data, isNull), column);
            Assert.AreEqual(Test.Result.OKAY, result, string.Format("Test {0} on column {1} failed on data '{2}', when expecting success.", test, column, data));
        }

        public void AssertError(Column column, Test test, string data)
        {
            var result = test.GetResult(new Post(data, false), column);
            Assert.AreEqual(Test.Result.ERROR, result, string.Format("Test {0} on column {1} succeeded on data '{2}', when expecting failure.", test, column, data));
        }

        [TestMethod]
        public void AnalyzeCharacterOverflowUnderflow()
        {
            var overflowTest = new Test.Overflow();
            var underflowTest = new Test.Underflow();

            uint length = 0;
            var colchar = new Column(null, null, new ParameterizedDataType(DataType.CHARACTER, Parameter.WithLength(length)), null, false, null, "c1", 1, null, null);
            var colvarchar = new Column(null, null, new ParameterizedDataType(DataType.CHARACTER_VARYING, Parameter.WithLength(length)), null, false, null, "c1", 1, null, null);
            var colnatchar = new Column(null, null, new ParameterizedDataType(DataType.NATIONAL_CHARACTER, Parameter.WithLength(length)), null, false, null, "c1", 1, null, null);
            var colnatvarchar = new Column(null, null, new ParameterizedDataType(DataType.NATIONAL_CHARACTER_VARYING, Parameter.WithLength(length)), null, false, null, "c1", 1, null, null);

            AssertOkay(colchar, overflowTest, "");
            AssertOkay(colvarchar, overflowTest, "");
            AssertOkay(colnatchar, overflowTest, "");
            AssertOkay(colnatvarchar, overflowTest, "");
            AssertError(colchar, overflowTest, "abc");
            AssertError(colvarchar, overflowTest, "abc");
            AssertError(colnatchar, overflowTest, "abc");
            AssertError(colnatvarchar, overflowTest, "abc");
            AssertOkay(colchar, underflowTest, "");
            AssertOkay(colvarchar, underflowTest, "");
            AssertOkay(colnatchar, underflowTest, "");
            AssertOkay(colnatvarchar, underflowTest, "");

            length = 8;
            colchar = new Column(null, null, new ParameterizedDataType(DataType.CHARACTER, Parameter.WithLength(length)), null, false, null, "c1", 1, null, null);
            colvarchar = new Column(null, null, new ParameterizedDataType(DataType.CHARACTER_VARYING, Parameter.WithLength(length)), null, false, null, "c1", 1, null, null);
            colnatchar = new Column(null, null, new ParameterizedDataType(DataType.NATIONAL_CHARACTER, Parameter.WithLength(length)), null, false, null, "c1", 1, null, null);
            colnatvarchar = new Column(null, null, new ParameterizedDataType(DataType.NATIONAL_CHARACTER_VARYING, Parameter.WithLength(length)), null, false, null, "c1", 1, null, null);

            AssertOkay(colchar, overflowTest, "abcdefg");
            AssertOkay(colvarchar, overflowTest, "abcdefg");
            AssertOkay(colnatchar, overflowTest, "abcdefg");
            AssertOkay(colnatvarchar, overflowTest, "abcdefg");

            AssertError(colchar, overflowTest, "abcdefghi");
            AssertError(colvarchar, overflowTest, "abcdefghi");
            AssertError(colnatchar, overflowTest, "abcdefghi");
            AssertError(colnatvarchar, overflowTest, "abcdefghi");

            AssertOkay(colchar, underflowTest, "abcdefgh");
            AssertOkay(colvarchar, underflowTest, "abcdefgh");
            AssertOkay(colnatchar, underflowTest, "abcdefgh");
            AssertOkay(colnatvarchar, underflowTest, "abcdefgh");

            AssertError(colchar, underflowTest, "abcd");
            AssertOkay(colvarchar, underflowTest, "abcd");
            AssertError(colnatchar, underflowTest, "abcd");
            AssertOkay(colnatvarchar, underflowTest, "abcd");
        }

        [TestMethod]
        public void AnalyzeBlankTest()
        {
            var test = new Test.Blank();

            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                var column = new Column(null, null, new ParameterizedDataType(dataType, Parameter.Default(dataType)), null, false, null, "c1", 1, null, null);
                AssertOkay(column, test, "abcdefgh!");
                AssertError(column, test, " abcdefgh!");
                AssertError(column, test, "\nabcdefgh!");
                AssertError(column, test, "\rabcdefgh!");
                AssertError(column, test, "\tabcdefgh!");
                AssertError(column, test, "abcdefgh! ");
                AssertError(column, test, "abcdefgh!\t");
                AssertError(column, test, "abcdefgh!\r");
                AssertError(column, test, "abcdefgh!\n");
            }
        }

        [TestMethod]
        public void AnalyzeDateFormatTest()
        {
            var test = Test.DateFormatTest();
            var column = new Column(null, "name", new ParameterizedDataType(DataType.DATE, null), "DATE", false, "desc", "c1", 1, null, null);

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
        public void AnalyzeTimeFormatTest()
        {
            var test = Test.TimeFormatTest();
            var column = new Column(null, "name", new ParameterizedDataType(DataType.TIME, Parameter.WithPrecision(10)), "TIME (10)", false, "desc", "c1", 1, null, null);

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
        public void AnalyzeTimeWithTimezoneFormatTest()
        {
            var test = Test.TimeWithTimeZoneTest();
            var column = new Column(null, "name", new ParameterizedDataType(DataType.TIME_WITH_TIME_ZONE, Parameter.WithPrecision(10)), "TIME WITH TIME ZONE (10)", false, "desc", "c1", 1, null, null);

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
        public void AnalyzeTimestampFormatTest()
        {
            var test = Test.TimestampFormatTest();
            var column = new Column(null, "name", new ParameterizedDataType(DataType.TIMESTAMP, Parameter.WithPrecision(10)), "TIMESTAMP (10)", false, "desc", "c1", 1, null, null);

            AssertOkay(column, test, "2000-01-01T14:15:20");
            AssertOkay(column, test, "1899-12-31T10:30:00");
            AssertOkay(column, test, "2018-01-03T17:30:00");
            AssertOkay(column, test, "2000-08-10T13:37:00");
            AssertOkay(column, test, "2000-08-10T00:00:00"); // minimum time
            AssertOkay(column, test, "2000-08-10T23:59:59"); // maximum time

            AssertError(column, test, "2000-08-10T14:15:20+05:00"); // contains correctly formatted time zone
            AssertError(column, test, "2000-08-10T14:15:20+5:00"); // contains incorrectly formatted time zonee
            AssertError(column, test, "2000-08-10T14:15:20 "); // with a blank
            AssertError(column, test, " 2000-08-10T14:15:20"); // with a blank
            AssertError(column, test, "2000-08-10T25:20:11"); // Hour out of range
            AssertError(column, test, "2000-08-10T1:11:11"); // 1-digit hour
            AssertError(column, test, "2000-08-10T111:11:11"); // 3-digit hour
            AssertError(column, test, "2000-08-10T10:63:30"); // minute out of range
            AssertError(column, test, "2000-08-10T10:5:30"); // 1-digit minute
            AssertError(column, test, "2000-08-10T10:555:30"); // 3-digit minute
            AssertError(column, test, "2000-08-10T10:10:99"); // second out of range
            AssertError(column, test, "2000-08-10T10:10:1"); // 1-digit second
            AssertError(column, test, "2000-08-10T10:10:123"); // 3-digit second
            AssertError(column, test, "abc"); // non-format string
            AssertError(column, test, ""); // empty string

            // dates with minimum time
            AssertOkay(column, test, "0001-01-01T00:00:00");
            AssertOkay(column, test, "2016-05-23T00:00:00");
            AssertOkay(column, test, "2016-05-23T00:00:00");
            AssertOkay(column, test, "2016-05-23T00:00:00");
            AssertOkay(column, test, "1994-12-31T00:00:00");
            AssertOkay(column, test, "2016-12-01T00:00:00");
            AssertOkay(column, test, "2017-01-31T00:00:00");
            AssertOkay(column, test, "1991-05-16T00:00:00");
            AssertOkay(column, test, "9999-12-31T00:00:00");

            // dates with maximum time
            AssertOkay(column, test, "0001-01-01T23:59:59");
            AssertOkay(column, test, "2016-05-23T23:59:59");
            AssertOkay(column, test, "2016-05-23T23:59:59");
            AssertOkay(column, test, "2016-05-23T23:59:59");
            AssertOkay(column, test, "1994-12-31T23:59:59");
            AssertOkay(column, test, "2016-12-01T23:59:59");
            AssertOkay(column, test, "2017-01-31T23:59:59");
            AssertOkay(column, test, "1991-05-16T23:59:59");
            AssertOkay(column, test, "9999-12-31T23:59:59");

            // dates with other time
            AssertOkay(column, test, "0001-01-01T14:15:20");
            AssertOkay(column, test, "2016-05-23T14:15:20");
            AssertOkay(column, test, "2016-05-23T14:15:20");
            AssertOkay(column, test, "2016-05-23T14:15:20");
            AssertOkay(column, test, "1994-12-31T14:15:20");
            AssertOkay(column, test, "2016-12-01T14:15:20");
            AssertOkay(column, test, "2017-01-31T14:15:20");
            AssertOkay(column, test, "1991-05-16T14:15:20");
            AssertOkay(column, test, "9999-12-31T14:15:20");

            AssertError(column, test, "99-1-31T14:15:20"); // two-digit year
            AssertError(column, test, "2017-12-1T14:15:20"); // one-digit-month
            AssertError(column, test, "2017-1-31T14:15:20"); // one-digit day
            AssertError(column, test, "abc"); // letters
            AssertError(column, test, "2016-15-23T14:15:20"); // Month out of range
            AssertError(column, test, "2016-06-80T14:15:20"); // Day out of range
            AssertError(column, test, "1905-10T14:15:20"); // Day missing
            AssertError(column, test, ""); // Empty string
            AssertError(column, test, "   "); // Blank string
        }

        [TestMethod]
        public void AnalyzeTimestampWithTimezoneFormatTest()
        {
            var test = Test.TimestampWithTimeZoneFormatTest();
            var column = new Column(null, "name", new ParameterizedDataType(DataType.TIMESTAMP_WITH_TIME_ZONE, Parameter.WithPrecision(10)), "TIMESTAMP WITH TIME ZONE (10)", false, "desc", "c1", 1, null, null);

            AssertOkay(column, test, "2000-08-10T14:15:20Z");
            AssertOkay(column, test, "2000-08-10T14:15:20+12:00");
            AssertOkay(column, test, "2000-08-10T14:15:20-12:00");
            AssertOkay(column, test, "2000-08-10T13:37:00+05:30");
            AssertOkay(column, test, "2000-08-10T00:00:00Z"); // minimum tz
            AssertOkay(column, test, "2000-08-10T23:59:59Z"); // maximum tz
            AssertOkay(column, test, "2000-08-10T00:00:00+00:00"); // minimum time with positive minimum tz
            AssertOkay(column, test, "2000-08-10T23:59:59+12:00"); // maximum time with positive maximum tz
            AssertOkay(column, test, "2000-08-10T23:59:59-00:00"); // minimum time with negative minimum tz
            AssertOkay(column, test, "2000-08-10T23:59:59-12:00"); // maximum time with negative maximum tz

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

            // dates with minimum time
            AssertOkay(column, test, "0001-01-01T00:00:00Z");
            AssertOkay(column, test, "2016-05-23T00:00:00Z");
            AssertOkay(column, test, "2016-05-23T00:00:00Z");
            AssertOkay(column, test, "2016-05-23T00:00:00Z");
            AssertOkay(column, test, "1994-12-31T00:00:00Z");
            AssertOkay(column, test, "2016-12-01T00:00:00Z");
            AssertOkay(column, test, "2017-01-31T00:00:00Z");
            AssertOkay(column, test, "1991-05-16T00:00:00Z");
            AssertOkay(column, test, "9999-12-31T00:00:00Z");

            // dates with maximum time
            AssertOkay(column, test, "0001-01-01T23:59:59Z");
            AssertOkay(column, test, "2016-05-23T23:59:59Z");
            AssertOkay(column, test, "2016-05-23T23:59:59Z");
            AssertOkay(column, test, "2016-05-23T23:59:59Z");
            AssertOkay(column, test, "1994-12-31T23:59:59Z");
            AssertOkay(column, test, "2016-12-01T23:59:59Z");
            AssertOkay(column, test, "2017-01-31T23:59:59Z");
            AssertOkay(column, test, "1991-05-16T23:59:59Z");
            AssertOkay(column, test, "9999-12-31T23:59:59Z");

            // dates with other time
            AssertOkay(column, test, "0001-01-01T14:15:20Z");
            AssertOkay(column, test, "2016-05-23T14:15:20Z");
            AssertOkay(column, test, "2016-05-23T14:15:20Z");
            AssertOkay(column, test, "2016-05-23T14:15:20Z");
            AssertOkay(column, test, "1994-12-31T14:15:20Z");
            AssertOkay(column, test, "2016-12-01T14:15:20Z");
            AssertOkay(column, test, "2017-01-31T14:15:20Z");
            AssertOkay(column, test, "1991-05-16T14:15:20Z");
            AssertOkay(column, test, "9999-12-31T14:15:20Z");

            AssertError(column, test, "99-1-31T14:15:20Z"); // two-digit year
            AssertError(column, test, "2017-12-1T14:15:20Z"); // one-digit-month
            AssertError(column, test, "2017-1-31T14:15:20Z"); // one-digit day
            AssertError(column, test, "abc"); // letters
            AssertError(column, test, "2016-15-23T14:15:20Z"); // Month out of range
            AssertError(column, test, "2016-06-80T14:15:20Z"); // Day out of range
            AssertError(column, test, "1905-10T14:15:20Z"); // Day missing
            AssertError(column, test, ""); // Empty string
            AssertError(column, test, "   "); // Blank string
        }

        [TestMethod]
        public void PatternTest()
        {
            var test = new Test.Pattern(new Regex(@"[a-zA-Z][0-9]"), m =>
            {
                foreach (Match match in m)
                {
                    if (match.Success)
                    {
                        return Test.Result.OKAY;
                    }
                }

                return Test.Result.ERROR;
            });
            var column = new Column(null, "name", new ParameterizedDataType(DataType.CHARACTER_VARYING, Parameter.WithLength(100)), "VARCHAR (100)", false, "desc", "c1", 1, null, null);
            AssertOkay(column, test, "a1");
            AssertOkay(column, test, "   b9   ");
            AssertError(column, test, "12");
            AssertError(column, test, "1a");
            AssertError(column, test, " 12 ");
            AssertError(column, test, "abcd 1abcd");
        }

        [TestMethod]
        [DeploymentItem(@"..\..\TestResources", @"TestResources")]
        public void Analyzer()
        {
            var AV = ArchiveVersion.Load(@"TestResources\AVID.OVERFLOW.1.1", new TestLogger());
            var Analyzer = new Analyzer(AV, AV.Tables, new TestLogger());

            var table1 = AV.Tables.First();
            var idColumn = table1.Columns[0];
            Assert.AreEqual(idColumn.ColumnIdNumber, 1);
            var nameColumn = table1.Columns[1];
            Assert.AreEqual(nameColumn.ColumnIdNumber, 2);
            var phoneColumn = table1.Columns[2];
            Assert.AreEqual(phoneColumn.ColumnIdNumber, 3);
            var timeColumn = table1.Columns[3];
            Assert.AreEqual(timeColumn.ColumnIdNumber, 4);

            foreach (var column in AV.Tables.First().Columns)
            {
                if (column.ColumnIdNumber != 1) // First column is integer
                {
                    Analyzer.AddTest(column, new Test.Overflow());
                }
            }

            Analyzer.TestHierachy[table1][phoneColumn].ForceCharacterType = true;

            Analyzer.MoveNextTable();
            Analyzer.InitializeTable();
            Analyzer.AnalyzeRows(3);

            foreach (var columnAnalysis in Analyzer.TestHierachy[table1].Values)
            {
                columnAnalysis.SuggestType();
            }

            Assert.AreEqual(0, Analyzer.TestHierachy[table1][idColumn].ErrorCount);
            Assert.AreEqual(1, Analyzer.TestHierachy[table1][nameColumn].ErrorCount);
            Assert.AreEqual(1, Analyzer.TestHierachy[table1][phoneColumn].ErrorCount);
            Assert.AreEqual(1, Analyzer.TestHierachy[table1][timeColumn].ErrorCount);

            Assert.AreEqual(1, Analyzer.TestHierachy[table1][nameColumn].SuggestedType.Parameter.CompareTo(nameColumn.ParameterizedDataType.Parameter));
            Assert.AreEqual(1, Analyzer.TestHierachy[table1][phoneColumn].SuggestedType.Parameter.CompareTo(phoneColumn.ParameterizedDataType.Parameter));
            Assert.AreEqual(1, Analyzer.TestHierachy[table1][timeColumn].SuggestedType.Parameter.CompareTo(timeColumn.ParameterizedDataType.Parameter));
        }

        public void AssertSuggestion(ParameterizedDataType before, ParameterizedDataType expected, params Post[] posts)
        {
            ColumnAnalysis columnAnalysis = new ColumnAnalysis(new Column(null, null, before, null, false, null, "c1", 1, null, null));
            foreach (var post in posts)
            {
                columnAnalysis.UpdateColumnStatistics(post);
                columnAnalysis.FirstRowAnalyzed |= true;
            }
            columnAnalysis.SuggestType();
            if (expected == null)
            {
                Assert.AreEqual(null, columnAnalysis.SuggestedType);
            }
            else
            {
                if (columnAnalysis.SuggestedType == null)
                {
                    Assert.Fail($"No type was suggested, but a suggestion of {expected.DataType} was expected.");
                }
                else
                {
                    Assert.AreEqual(expected.DataType, columnAnalysis.SuggestedType.DataType);
                    if (expected.Parameter == null)
                    {
                        Assert.AreEqual(null, columnAnalysis.SuggestedType.Parameter);
                    }
                    else
                    {
                        Assert.AreEqual(0, expected.Parameter.CompareTo(columnAnalysis.SuggestedType.Parameter));
                    }
                }
            }
        }

        [TestMethod]
        public void SuggestTimestamp()
        {
            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                AssertSuggestion(new ParameterizedDataType(dataType, Parameter.Default(dataType)), new ParameterizedDataType(DataType.TIMESTAMP, Parameter.WithPrecision(10)),
                    new Post("2007-01-18T12:54:10.123", false),
                    new Post("", true),
                    new Post("2003-01-30T12:32:30", false),
                    new Post("2007-01-18T12:54:36.123", false),
                    new Post("2007-01-18T12:50:73.1231231234", false));
            }
        }

        [TestMethod]
        public void SuggestTimestampWithTimeZone()
        {
            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                AssertSuggestion(new ParameterizedDataType(dataType, Parameter.Default(dataType)), new ParameterizedDataType(DataType.TIMESTAMP_WITH_TIME_ZONE, Parameter.WithPrecision(10)),
                    new Post("2007-01-18T12:54:10.123Z", false),
                    new Post("", true),
                    new Post("2003-01-30T12:32:30+03:00", false),
                    new Post("2007-01-18T12:54:36.123-02:00", false),
                    new Post("2007-01-18T12:50:73.1231231234Z", false),
                    new Post("2010-11-20T12:54:63.123Z", false));
            }
        }

        [TestMethod]
        public void SuggestTime()
        {
            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                AssertSuggestion(new ParameterizedDataType(dataType, Parameter.Default(dataType)), new ParameterizedDataType(DataType.TIME, Parameter.WithPrecision(10)),
                    new Post("12:54:10.123", false),
                    new Post("12:32:30", false),
                    new Post("12:54:36.123", false),
                    new Post("12:50:73.1231231234", false));
            }
        }

        [TestMethod]
        public void SuggestTimeWithTimeZone()
        {
            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                AssertSuggestion(new ParameterizedDataType(dataType, Parameter.Default(dataType)), new ParameterizedDataType(DataType.TIME_WITH_TIME_ZONE, Parameter.WithPrecision(10)),
                    new Post("12:54:10.123Z", false),
                    new Post("12:32:30+03:00", false),
                    new Post("12:54:36.123-02:00", false),
                    new Post("", true),
                    new Post("12:50:73.1231231234Z", false),
                    new Post("12:54:63.123Z", false));
            }
        }

        [TestMethod]
        public void SuggestInteger()
        {
            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                if (dataType == DataType.INTEGER)
                    continue;

                AssertSuggestion(new ParameterizedDataType(dataType, Parameter.Default(dataType)), new ParameterizedDataType(DataType.INTEGER, null),
                    new Post("12", false),
                    new Post("0", false),
                    new Post("", true),
                    new Post("-100009", false),
                    new Post("342356", false),
                    new Post("0010", false));
            }
        }

        [TestMethod]
        public void SuggestNumeric()
        {
            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                AssertSuggestion(new ParameterizedDataType(dataType, Parameter.Default(dataType)),
                    new ParameterizedDataType(dataType == DataType.DECIMAL || dataType == DataType.NUMERIC ? dataType : DataType.DECIMAL, Parameter.WithPrecisionAndScale(9, 5)),
                    new Post("12", false),
                    new Post("0", false),
                    new Post("0.134", false),
                    new Post("-100009", false),
                    new Post("342356", false),
                    new Post("180001.12", false),
                    new Post("0010", false),
                    new Post("1200.01234", false),
                    new Post("", true));
            }
        }

        [TestMethod]
        public void SuggestDate()
        {
            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                if (dataType == DataType.DATE)
                    continue;

                AssertSuggestion(new ParameterizedDataType(dataType, Parameter.Default(dataType)),
                    new ParameterizedDataType(DataType.DATE, null),
                    new Post("2002-01-31", false),
                    new Post("1986-06-23", false),
                    new Post("2005-10-01", false),
                    new Post("2014-07-12", false),
                    new Post("2020-12-03", false),
                    new Post("", true));
            }
        }

        [TestMethod]
        public void SuggestCharacter()
        {
            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                AssertSuggestion(new ParameterizedDataType(dataType, Parameter.Default(dataType)),
                    new ParameterizedDataType(dataType == DataType.NATIONAL_CHARACTER || dataType == DataType.NATIONAL_CHARACTER_VARYING ? DataType.NATIONAL_CHARACTER : DataType.CHARACTER, Parameter.WithLength(11)),
                    new Post("110392-5050", false),
                    new Post("020890-8505", false),
                    new Post("100290-8724", false),
                    new Post("031185-1157", false),
                    new Post("120579-6933", false),
                    new Post("", true));
            }
        }

        [TestMethod]
        public void SuggestCharacterVarying()
        {
            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                AssertSuggestion(new ParameterizedDataType(dataType, Parameter.Default(dataType)),
                    new ParameterizedDataType(dataType == DataType.NATIONAL_CHARACTER || dataType == DataType.NATIONAL_CHARACTER_VARYING ? DataType.NATIONAL_CHARACTER_VARYING : DataType.CHARACTER_VARYING, Parameter.WithLength(10)),
                    new Post("2002-01-31", false),
                    new Post("12345", false),
                    new Post("abcdf", false),
                    new Post("a", false),
                    new Post("test!", false),
                    new Post("", true));
            }
        }

        [TestMethod]
        public void SuggestAllNull()
        {
            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                AssertSuggestion(new ParameterizedDataType(dataType, Parameter.Default(dataType)), null,
                    new Post("", true),
                    new Post("", true),
                    new Post("", true),
                    new Post("", true),
                    new Post("", true),
                    new Post("", true));
            }
        }

        [TestMethod]
        public void SuggestBoolean01()
        {
            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                AssertSuggestion(new ParameterizedDataType(dataType, Parameter.Default(dataType)),
                    dataType == DataType.BOOLEAN ? null : new ParameterizedDataType(DataType.BOOLEAN, null),
                    new Post("0", false),
                    new Post("1", false),
                    new Post("0", false),
                    new Post("", true),
                    new Post("1", false),
                    new Post("1", false));
            }
        }

        [TestMethod]
        public void SuggestBooleanTrueFalse()
        {
            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                AssertSuggestion(new ParameterizedDataType(dataType, Parameter.Default(dataType)),
                    dataType == DataType.BOOLEAN ? null : new ParameterizedDataType(DataType.BOOLEAN, null),
                    new Post("true", false),
                    new Post("false", false),
                    new Post("", true),
                    new Post("false", false),
                    new Post("false", false),
                    new Post("true", false),
                    new Post("", true));
            }
        }


        [TestMethod]
        public void SuggestBooleanMixed01TrueFalse()
        {
            foreach (DataType dataType in Enum.GetValues(typeof(DataType)))
            {
                AssertSuggestion(new ParameterizedDataType(dataType, Parameter.Default(dataType)),
                    dataType == DataType.BOOLEAN ? null : new ParameterizedDataType(DataType.BOOLEAN, null),
                    new Post("true", false),
                    new Post("false", false),
                    new Post("1", false),
                    new Post("", true),
                    new Post("false", false),
                    new Post("false", false),
                    new Post("0", false),
                    new Post("true", false),
                    new Post("", true));
            }
        }
        //string assertText = System.IO.File.ReadAllText(@"W:\Xml-tag\assertText.txt");
        [TestMethod]
        public void SuspiciosKeyword_nokeyword_message_isnull()
        {
            string assertText = "ad3aed2a - 20be - 11e3 - aa74 - 3cd92bf42f50";

            var test = new Test.SuspiciousKeyword();
            INotification noti_kw = null;
            var kwTest = test.Run(new Post(assertText, false), null, myNoti => noti_kw = myNoti);

            Assert.IsNull(noti_kw.Message);
        }

        [TestMethod]
        public void SuspiciosKeyword_nokeyword_message_isnotemptystr()
        {
            string assertText = "ad3aed2a - 20be - 11e3 - aa74 - 3cd92bf42f50";

            var test = new Test.SuspiciousKeyword();
            INotification noti_kw = null;
            var kwTest = test.Run(new Post(assertText, false), null, myNoti => noti_kw = myNoti);

            Assert.AreNotEqual(noti_kw.Message, "");
        }

        [TestMethod]
        public void SuspiciosKeyword_nokeyword_testtype_notequal_unallowed_keyword()
        {
            string assertText = "ad3aed2a - 20be - 11e3 - aa74 - 3cd92bf42f50";

            var test = new Test.SuspiciousKeyword();
            INotification noti_kw = null;
            var kwTest = test.Run(new Post(assertText, false), null, myNoti => noti_kw = myNoti);

            Assert.AreNotEqual($"Test ({AnalysisTestType.UNALLOWED_KEYWORD})", noti_kw.Header);
        }


        [TestMethod]
        public void SuspiciosKeyword_span_font_style_margin_resulterror()
        {
            string assertText = "se nedenstående notat.p class=MsoNormal style=margin: 0cm 0cm 0pt 26.95pt; span style=font - family: Times New Roman;";

            var test = new Test.SuspiciousKeyword();
            INotification noti_kw = null;
            var kwTest = test.Run(new Post(assertText, false), null, myNoti => noti_kw = myNoti);

            Assert.AreEqual(kwTest, Test.Result.ERROR);
        }

        [TestMethod]
        public void HtmlEntity_opening_tag_result_error()
        {
            var test = new Test.HtmlEntity();
            INotification noti_otag = null;
            var optag = test.Run(new Post("aeio<span>rgroi", false), null, myNoti => noti_otag = myNoti);
            Assert.AreEqual(optag, Test.Result.ERROR);
        }

        [TestMethod]
        public void HtmlEntity_openingtag_update_message()
        {
            var test = new Test.HtmlEntity();
            INotification noti_otag = null;
            var optag = test.Run(new Post("aeio<span>rgroi", false), null, myNoti => noti_otag = myNoti);
            Assert.AreEqual("<span>", noti_otag.Message);
        }

        [TestMethod]
        public void HtmlEntity_closingtag_result_error()
        {
            var test = new Test.HtmlEntity();
            INotification noti_ctag = null;
            var closetag = test.Run(new Post("aeio</span>rgroi", false), null, myNoti => noti_ctag = myNoti);
            Assert.AreEqual(closetag, Test.Result.ERROR);
        }

        [TestMethod]
        public void HtmlEntity_closingtag_update_message()
        {
            var test = new Test.HtmlEntity();
            INotification noti_ctag = null;
            var closetag = test.Run(new Post("aeio</span>rgroi", false), null, myNoti => noti_ctag = myNoti);
            Assert.AreEqual("</span>", noti_ctag.Message);
        }

        [TestMethod]
        public void EntityCharRef_unallowed_hexacharref_result_error()
        {
            var test = new Test.EntityCharRef();
            INotification noti = null;
            var res = test.Run(new Post(".Socialt&#x0A;Kl. har boet sammen", false), null, myNoti => noti = myNoti);
            Assert.AreEqual(res, Test.Result.ERROR);
        }

        [TestMethod]
        public void EntityCharRef_unallowed_hexacharref_equal_message()
        {
            var test = new Test.EntityCharRef();
            INotification noti = null;
            var res1 = test.Run(new Post(".Socialt&#x0A;Kl. har boet sammen", false), null, myNoti => noti = myNoti);
            Assert.AreEqual("&#x0A;", noti.Message);
        }

        [TestMethod]
        public void EntityCharRef_allowed_charref_result_ok()
        {
            var test = new Test.EntityCharRef();
            INotification noti_res2 = null;
            var res2 = test.Run(new Post("borg&amp;Anja var her igår og steg til 20 mg. ", false), null, myNoti => noti_res2 = myNoti);
            Assert.AreEqual(res2, Test.Result.OKAY);
        }

        [TestMethod]
        public void EntityCharRef_allowed_charref_notification_isnull()
        {
            var test = new Test.EntityCharRef();
            INotification noti_res2 = null;
            var res2 = test.Run(new Post("borg&amp;Anja var her igår og steg til 20 mg. ", false), null, myNoti => noti_res2 = myNoti);
            Assert.IsNull(noti_res2);
        }

        [TestMethod]
        public void RepeatingChar_ychar_result_error()
        {
            var test = new Test.RepeatingChar();
            INotification noti = null;
            var rep = test.Run(new Post("ÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿ", false), null, myNotification => noti = myNotification);
            Assert.AreEqual(rep, Test.Result.ERROR);
        }

        [TestMethod]
        public void RepeatingChar_ychar_update_message()
        {
            var test = new Test.RepeatingChar();
            INotification noti = null;
            var rep = test.Run(new Post("ÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿÿ", false), null, myNotification => noti = myNotification);
            Assert.AreEqual("ÿ", noti.Message);
        }


        [TestMethod]
        public void NotificationsTest()
        {
            var notifTest = new Test.EntityCharRef();
            INotification noti = null;
            var test = notifTest.Run(new Post("&#x0A;", false), null, myNotification => noti = myNotification);
            Assert.AreEqual(test, Test.Result.ERROR);
            Assert.IsNotNull(noti);
            Assert.AreEqual("&#x0A;", noti.Message);
        }
    }
}
