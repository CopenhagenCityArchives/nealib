using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using HardHorn.ArchiveVersion;
using System.Text.RegularExpressions;
using System.Dynamic;
using HardHorn.Logging;

namespace HardHorn.Analysis
{
    public enum AnalysisErrorType
    {
        OVERFLOW,
        UNDERFLOW,
        MISMATCH,
        FORMAT,
        NULL,
        BLANK
    }

    public class AnalysisError
    {
        int _count = 0;
        public int Count { get { return _count; } }
        List<ExpandoObject> _instances = new List<ExpandoObject>();

        public IEnumerable<ExpandoObject> Instances { get { return _instances as IEnumerable<ExpandoObject>; } }
        public AnalysisErrorType Type { get; private set; }

        public AnalysisError(AnalysisErrorType type)
        {
            Type = type;
        }

        public void Add(int line, int pos, string data)
        {
            if (_count < AnalysisReport.MAX_ERROR_INSTANCES)
            {
                dynamic instance = new ExpandoObject();
                instance.Data = data;
                instance.Line = line;
                instance.Pos = pos;
                _instances.Add(instance);
            }
            _count++;
        }

        public override string ToString()
        {
            string repr = string.Format("{{{0}, count={1}}}", Type, Count);
            foreach (dynamic instance in _instances)
            {
                repr += Environment.NewLine + string.Format("[{0},{1}]({2})", instance.Line, instance.Pos, instance.Data);
            }
            return repr;
        }
    }

    public class AnalysisReport
    {
        public int ErrorCount { get; private set; }
        public Column Column { get; set; }
        public Dictionary<AnalysisErrorType, AnalysisError> Errors { get; set; }
        public int[] MinLengths { get; set; }
        public int[] MaxLengths { get; set; }
        public static readonly int MAX_ERROR_INSTANCES = 10;
        public Tuple<DataType, DataTypeParam> SuggestedType { get; set; }
        public bool AnalysisFirstRow { get; set; }

        public AnalysisReport(Column column)
        {
            AnalysisFirstRow = false;
            Column = column;
            ErrorCount = 0;
            MinLengths = new int[column.Param != null ? column.Param.Length : 1];
            MaxLengths = new int[column.Param != null ? column.Param.Length : 1];
            Errors = new Dictionary<AnalysisErrorType, AnalysisError>();
        }

        public void ReportError(int line, int pos, AnalysisErrorType errorType, string instance)
        {
            ErrorCount++;
            if (!Errors.ContainsKey(errorType))
            {
                Errors.Add(errorType, new AnalysisError(errorType));
            }

            Errors[errorType].Add(line, pos, instance);
        }

        public void SuggestType()
        {
            switch (Column.Type)
            {
                case DataType.CHARACTER:
                    if (MinLengths[0] == MaxLengths[0] && MaxLengths[0] > Column.Param[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.CHARACTER, new DataTypeParam(MaxLengths[0]));
                    }
                    else if (MinLengths[0] != MaxLengths[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.CHARACTER_VARYING, new DataTypeParam(MaxLengths[0]));
                    }
                    break;
                case DataType.NATIONAL_CHARACTER:
                    if (MinLengths[0] == MaxLengths[0] && MaxLengths[0] > Column.Param[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.NATIONAL_CHARACTER, new DataTypeParam(MaxLengths[0]));
                    }
                    else if (MinLengths[0] != MaxLengths[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.NATIONAL_CHARACTER_VARYING, new DataTypeParam(MaxLengths[0]));
                    }
                    break;
                case DataType.CHARACTER_VARYING:
                    if (MinLengths[0] == MaxLengths[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.CHARACTER, new DataTypeParam(MaxLengths[0]));
                    }
                    else if (MaxLengths[0] != Column.Param[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.CHARACTER_VARYING, new DataTypeParam(MaxLengths[0]));
                    }
                    break;
                case DataType.NATIONAL_CHARACTER_VARYING:
                    if (MinLengths[0] == MaxLengths[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.NATIONAL_CHARACTER, new DataTypeParam(MaxLengths[0]));
                    }
                    else if (MaxLengths[0] != Column.Param[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.NATIONAL_CHARACTER_VARYING, new DataTypeParam(MaxLengths[0]));
                    }
                    break;
                case DataType.DECIMAL:
                    if (MaxLengths[0] + MaxLengths[1] != Column.Param[0] || MaxLengths[1] != Column.Param[1])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.DECIMAL, new DataTypeParam(new int[] { MaxLengths[0] + MaxLengths[1], MaxLengths[1] }));
                    }
                    break;
            }
        }

        public override string ToString()
        {
            string repr = string.Format("[{0}, {1}{2}{3}]",
                Column.Name,
                Column.Type.ToString(),
                Column.Param != null && Column.Param.Length > 0 ? "(" + string.Join(",", Column.Param) + ")" : string.Empty,
                Column.Nullable ? ", nullable" : string.Empty,
                ErrorCount);

            foreach (var errorType in Errors.Keys)
            {
                var errors = Errors[errorType];
                if (errors.Count > 0)
                {
                    repr += Environment.NewLine + string.Join(Environment.NewLine, errors);
                }
            }
            return repr;
        }
    }

    public class DataAnalyzer
    {
        public IEnumerable<Table> Tables { get; set; }
        public Dictionary<DataType, HashSet<AnalysisErrorType>> TestSelection { get; set; }
        public IEnumerable<RegexTest> RegexTests { get; set; }
        ILogger _log;

        string _location;
        XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";
        XNamespace xmlnsxsi = "http://www.w3.org/2001/XMLSchema-instance";

        Table currentTable;
        FileStream tableStream;
        XmlReader tableReader;

        Regex timestamp_regex = new Regex(@"(\d\d\d\d)-(\d\d)-(\d\d)T(\d\d):(\d\d):(\d\d)(?:.(\d+))?");
        Regex date_regex = new Regex(@"(\d\d\d\d)-(\d\d)-(\d\d)");
        Regex time_regex = new Regex(@"(\d\d):(\d\d):(\d\d)(?:.(\d+))?");
        static int[] months = new int[] { 31, 29, 31, 30, 31, 30, 31, 33, 30, 31, 30, 31 };

        public Dictionary<string, Dictionary<string, AnalysisReport>> Report { get; private set; }

        public DataAnalyzer(string location, ILogger log)
        {
            _log = log;
            TestSelection = new Dictionary<DataType, HashSet<AnalysisErrorType>>();
            _location = location;
            var tableIndexDocument = XDocument.Load(Path.Combine(_location, "Indices", "tableIndex.xml"));

            Report = new Dictionary<string, Dictionary<string, AnalysisReport>>();

            Tables = new List<Table>();

            var xtables = tableIndexDocument.Descendants(xmlns + "tables").First();
            foreach (var xtable in xtables.Elements(xmlns + "table"))
            {
                Table table = Table.Parse(xmlns, xtable, _log);
                (Tables as List<Table>).Add(table);
            }

            PrepareReports();
        }

        public void PrepareReports()
        {
            Report.Clear();
            foreach (var table in Tables)
            {
                var columnReports = new Dictionary<string, AnalysisReport>();
                foreach (var column in table.Columns)
                {
                    columnReports.Add(column.Name, new AnalysisReport(column));
                }
                Report.Add(table.Name, columnReports);
            }
        }

        public int AnalyzeRows(int n = 100000)
        {
            dynamic rows = new ExpandoObject[currentTable.Columns.Count, n];
            int row = 0;

            while (tableReader.Read() && row < n)
            {
                if (tableReader.NodeType == XmlNodeType.Element && tableReader.Name.Equals("row"))
                {
                    using (XmlReader inner = tableReader.ReadSubtree())
                    {
                        if (inner.Read())
                        {
                            var xrow = XElement.Load(inner);
                            var xdatas = xrow.Elements();

                            int i = 0;
                            foreach (var xdata in xdatas)
                            {
                                if (i > currentTable.Columns.Count)
                                {
                                    throw new InvalidOperationException("Data file and column mismatch.");
                                }
                                var xmlInfo = tableReader as IXmlLineInfo;
                                var isNull = false;
                                if (xdata.HasAttributes)
                                {
                                    var xnull = xdata.Attribute(xmlnsxsi + "nil");
                                    bool.TryParse(xnull.Value, out isNull);
                                }
                                dynamic instance = new ExpandoObject();
                                instance.Line = xmlInfo.LineNumber;
                                instance.Pos = xmlInfo.LinePosition;
                                instance.IsNull = isNull;
                                instance.Data = xdata.Value;
                                rows[i, row] = instance;
                                i++;
                            }

                            row++;
                        }
                    }
                }
            }

            // analyize the rows
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < currentTable.Columns.Count; j++)
                {
                    AnalyzeLengths(Report[currentTable.Name][currentTable.Columns[j].Name], rows[j, i].Data);
                    AnalyzeData(rows[j, i].Line,
                        rows[j, i].Pos,
                        currentTable.Columns[j],
                        rows[j, i].Data,
                        rows[j, i].IsNull,
                        Report[currentTable.Name][currentTable.Columns[j].Name]);
                }
            }

            return row; // return true if all specified rows were read
        }

        public void InitializeTableAnalysis(Table table)
        {
            tableStream = new FileStream(Path.Combine(_location, "Tables", table.Folder, table.Folder + ".xml"), FileMode.Open, FileAccess.Read);
            tableReader = XmlReader.Create(tableStream);
            currentTable = table;
        }

        public void DisposeTableAnalysis()
        {
            tableReader.Close();
            tableReader.Dispose();
            tableStream.Close();
            tableStream.Dispose();
        }

        void AnalyzeLengths(AnalysisReport report, string data)
        {
            switch (report.Column.Type)
            {
                case DataType.NATIONAL_CHARACTER:
                case DataType.CHARACTER:
                    if (report.AnalysisFirstRow)
                    {
                        report.MinLengths[0] = Math.Min(report.MinLengths[0], data.Length);
                        report.MaxLengths[0] = Math.Max(report.MaxLengths[0], data.Length);
                    }
                    else
                    {
                        report.MinLengths[0] = data.Length;
                        report.MaxLengths[0] = data.Length;
                    }
                    break;
                case DataType.NATIONAL_CHARACTER_VARYING:
                case DataType.CHARACTER_VARYING:
                    if (report.AnalysisFirstRow)
                    {
                        report.MinLengths[0] = Math.Min(report.MinLengths[0], data.Length);
                        report.MaxLengths[0] = Math.Max(report.MaxLengths[0], data.Length);
                    }
                    else
                    {
                        report.MinLengths[0] = data.Length;
                        report.MaxLengths[0] = data.Length;
                    }
                    break;
                case DataType.DECIMAL:
                    var components = data.Split('.');
                    if (report.AnalysisFirstRow)
                    {
                        report.MinLengths[0] = Math.Min(report.MinLengths[0], components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                        report.MaxLengths[0] = Math.Max(report.MaxLengths[0], components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                        report.MinLengths[1] = Math.Min(report.MinLengths[1], components.Length == 1 ? 0 : components[1].Length);
                        report.MaxLengths[1] = Math.Max(report.MaxLengths[1], components.Length == 1 ? 0 : components[1].Length);
                    }
                    else
                    {
                        report.MinLengths[0] = components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length;
                        report.MaxLengths[0] = components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length;
                        report.MinLengths[1] = components.Length == 1 ? 0 : components[1].Length;
                        report.MaxLengths[1] = components.Length == 1 ? 0 : components[1].Length;
                    }
                    break;
                case DataType.TIME:
                case DataType.DATE:
                case DataType.TIMESTAMP:
                    break;
            }
        }

        void AnalyzeData(int line, int pos, Column column, string data, bool isNull, AnalysisReport report)
        {
            if (RegexTests != null)
            {
                foreach (var regexTest in RegexTests)
                {
                    if (regexTest.ShouldPerformMatch(column) && !isNull && !regexTest.MatchData(data))
                    {
                        report.ReportError(line, pos, AnalysisErrorType.FORMAT, data);
                    }
                }
            }

            Match match;
            if (!TestSelection.ContainsKey(column.Type))
            {
                return;
            }
            var currentTests = TestSelection[column.Type];
            if (currentTests.Count == 0)
            {
                return;
            }

            switch (column.Type)
            {
                case DataType.NATIONAL_CHARACTER:
                case DataType.CHARACTER:
                    if (currentTests.Contains(AnalysisErrorType.UNDERFLOW) && data.Length < column.Param[0])
                    {
                        report.ReportError(line, pos, AnalysisErrorType.UNDERFLOW, data);
                    }
                    if (currentTests.Contains(AnalysisErrorType.OVERFLOW) && data.Length > column.Param[0])
                    {
                        report.ReportError(line, pos, AnalysisErrorType.OVERFLOW, data);
                    }
                    break;
                case DataType.NATIONAL_CHARACTER_VARYING:
                case DataType.CHARACTER_VARYING:
                    // Data too long
                    if (currentTests.Contains(AnalysisErrorType.OVERFLOW) && data.Length > column.Param[0])
                    {
                        report.ReportError(line, pos, AnalysisErrorType.OVERFLOW, data);
                    }
                    break;
                case DataType.DECIMAL:
                    var components = data.Split('.');
                    // No separator
                    if (currentTests.Contains(AnalysisErrorType.OVERFLOW) && components.Length == 1)
                    {
                        if (components[0].Length > column.Param[0])
                        {
                            report.ReportError(line, pos, AnalysisErrorType.OVERFLOW, data);
                        }
                    }
                    // With separator
                    if (currentTests.Contains(AnalysisErrorType.OVERFLOW) && components.Length == 2)
                    {
                        if (components[0].Length + components[1].Length > column.Param[0] || components[1].Length > column.Param[1])
                        {
                            report.ReportError(line, pos, AnalysisErrorType.OVERFLOW, data);
                        }
                    }
                    break;
                case DataType.TIME:
                    match = date_regex.Match(data);
                    if (match.Success)
                    {
                        if (currentTests.Contains(AnalysisErrorType.OVERFLOW) &&
                            column.Param != null && match.Groups.Count == 8 && match.Groups[4].Length > column.Param[0])
                        {
                            report.ReportError(line, pos, AnalysisErrorType.OVERFLOW, data);
                        }
                        int year = int.Parse(match.Groups[1].Value);
                        int month = int.Parse(match.Groups[2].Value);
                        int day = int.Parse(match.Groups[3].Value);
                        if (currentTests.Contains(AnalysisErrorType.FORMAT) && invalidDate(year, month, day))
                        {
                            report.ReportError(line, pos, AnalysisErrorType.FORMAT, data);
                        }
                    }
                    else if (currentTests.Contains(AnalysisErrorType.FORMAT) && !(data.Length == 0 && column.Nullable && isNull))
                    {
                        report.ReportError(line, pos, AnalysisErrorType.FORMAT, data);
                    }
                    break;
                case DataType.DATE:
                    match = date_regex.Match(data);
                    if (match.Success)
                    {
                        int year = int.Parse(match.Groups[1].Value);
                        int month = int.Parse(match.Groups[2].Value);
                        int day = int.Parse(match.Groups[3].Value);
                        if (currentTests.Contains(AnalysisErrorType.FORMAT) && invalidDate(year, month, day))
                        {
                            report.ReportError(line, pos, AnalysisErrorType.FORMAT, data);
                        }
                    }
                    else if (currentTests.Contains(AnalysisErrorType.FORMAT) && !(data.Length == 0 && column.Nullable && isNull))
                    {
                        report.ReportError(line, pos, AnalysisErrorType.FORMAT, data);
                    }
                    break;
                case DataType.TIMESTAMP:
                    match = timestamp_regex.Match(data);
                    if (match.Success)
                    {
                        if (currentTests.Contains(AnalysisErrorType.OVERFLOW) &&
                            column.Param != null && match.Groups.Count == 8 && match.Groups[7].Length > column.Param[0])
                        {
                            report.ReportError(line, pos, AnalysisErrorType.OVERFLOW, data);
                        }

                        if (currentTests.Contains(AnalysisErrorType.FORMAT))
                        {
                            // validate datetime
                            int year = int.Parse(match.Groups[1].Value);
                            int month = int.Parse(match.Groups[2].Value);
                            int day = int.Parse(match.Groups[3].Value);
                            int hour = int.Parse(match.Groups[4].Value);
                            int minute = int.Parse(match.Groups[5].Value);
                            int second = int.Parse(match.Groups[6].Value);

                            if (invalidDate(year, month, day) || invalidTime(hour, minute, second))
                            {
                                report.ReportError(line, pos, AnalysisErrorType.FORMAT, data);
                            }
                        }
                    }
                    else if (currentTests.Contains(AnalysisErrorType.FORMAT) && !(data.Length == 0 && column.Nullable && isNull))
                    {
                        report.ReportError(line, pos, AnalysisErrorType.FORMAT, data);
                    }
                    break;
            }

            if (currentTests.Contains(AnalysisErrorType.BLANK) && data.Length > 0 &&
                (data[0] == ' ' ||
                 data[0] == '\n' ||
                 data[0] == '\t' ||
                 data[data.Length - 1] == ' ' ||
                 data[data.Length - 1] == '\n' ||
                 data[data.Length - 1] == '\t'))
            {
                report.ReportError(line, pos, AnalysisErrorType.BLANK, data);
            }

            report.AnalysisFirstRow = true;
        }

        bool invalidTime(int hour, int minute, int second)
        {
            return hour > 23 ||
                   hour < 0 ||
                   minute > 59 ||
                   minute < 0 ||
                   second > 60 ||
                   second < 0;
        }

        bool invalidDate(int year, int month, int day)
        {
            bool leap = (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
            return year <= 0 ||
                   month > 12 && month < 1 ||
                   day < 1 ||
                   month == 2 && leap && day > months[2] - 1 ||
                   day > months[month - 1];
        }
    }

    public class RegexTest
    {
        public Regex Regex { get; private set; }
        public Dictionary<string, HashSet<string>> Columns { get; private set; }

        public RegexTest(string regex, Dictionary<string, HashSet<string>> columns)
        {
            Regex = new Regex(regex);
            Columns = columns;
        }

        public bool ShouldPerformMatch(Column column)
        {
            return Columns.ContainsKey(column.Table.Name) && Columns[column.Table.Name].Contains(column.Name);
        }

        public bool MatchData(string data)
        {
            var match = Regex.Match(data);
            return match.Success;
        }
    }
}
