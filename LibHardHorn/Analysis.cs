using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using HardHorn.ArchiveVersion;

namespace HardHorn.Analysis
{
    public enum AnalysisErrorType
    {
        TOO_LONG,
        NOT_EXACT,
        MISMATCH,
        NOT_NULLABLE,
        NULL_NOT_EMPTY
    }

    public class AnalysisError
    {
        int _count = 0;
        public int Count { get { return _count; } }
        List<Tuple<int, int, string>> _instances = new List<Tuple<int, int, string>>();
        public AnalysisErrorType Type { get; private set; }

        public AnalysisError(AnalysisErrorType type)
        {
            Type = type;
        }

        public void Add(int line, int pos,string instance)
        {
            if (_count < AnalysisReport.MAX_ERROR_INSTANCES)
            {
                _instances.Add(new Tuple<int, int, string>(line, pos, instance));
            }
            _count++;
        }

        public override string ToString()
        {
            string repr = string.Format("{{{0}, count={1}}}", Type, Count);
            foreach (var instance in _instances)
            {
                repr += Environment.NewLine + string.Format("[{0},{1}]({2})", instance.Item1, instance.Item2, instance.Item3);
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

        public AnalysisReport(Column column)
        {
            Column = column;
            ErrorCount = 0;
            MinLengths = new int[column.Param != null ? column.Param.Length : 1];
            MaxLengths = new int[column.Param != null ? column.Param.Length : 1];
            Errors = new Dictionary<AnalysisErrorType, AnalysisError>();
            foreach (var errorType in Enum.GetValues(typeof(AnalysisErrorType)).Cast<AnalysisErrorType>())
            {
                Errors.Add(errorType, new AnalysisError(errorType));
            }
        }

        public void ReportError(int line, int pos, AnalysisErrorType errorType, string instance)
        {
            ErrorCount++;
            Errors[errorType].Add(line, pos, instance);
        }

        public override string ToString()
        {
            string repr = string.Format("[{0}, {1}{2}, nullable={3}]", 
                Column.Name,
                Column.Type.ToString(), 
                Column.Param.Length > 0 ? "(" + string.Join(",", Column.Param) + ")" : string.Empty,
                Column.Nullable,
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

        string _location;
        XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

        Dictionary<string, Dictionary<string, AnalysisReport>> report;

        public DataAnalyzer(string location, TextWriter writer)
        {
            _location = location;
            var tableIndexDocument = XDocument.Load(Path.Combine(_location, "Indices", "tableIndex.xml"));

            report = new Dictionary<string, Dictionary<string, AnalysisReport>>();

            List<Table> tables = new List<Table>();

            var xtables = tableIndexDocument.Descendants(xmlns + "tables").First();
            foreach (var xtable in xtables.Elements(xmlns + "table"))
            {
                Table table;
                if (Table.TryParse(xmlns, xtable, out table))
                {
                    tables.Add(table);
                    var columnReports = new Dictionary<string, AnalysisReport>();
                    foreach (var column in table.Columns)
                    {
                        columnReports.Add(column.Name, new AnalysisReport(column));
                    }
                    report.Add(table.Name, columnReports);
                }
            }

            foreach (var table in tables)
            {
                writer.WriteLine(table.Name);
                AnalyzeTable(table);
                foreach (var column in table.Columns)
                {
                    var columnReport = report[table.Name][column.Name];
                    if (columnReport.ErrorCount > 0)
                    {
                        writer.WriteLine(columnReport);
                    }
                }
            }
        }

        void AnalyzeTable(Table table)
        {
            using (FileStream stream = new FileStream(Path.Combine(_location, "Tables", table.Folder, table.Folder + ".xml"), FileMode.Open, FileAccess.Read))
            {
                using (XmlReader reader = XmlReader.Create(stream))
                {
                    var rows = new List<Tuple<int, int, string>[]>();

                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name.Equals("row"))
                        {
                            using (XmlReader inner = reader.ReadSubtree())
                            {
                                if (inner.Read())
                                {
                                    if (rows.Count == 10000)
                                    {
                                        foreach (var drow in rows)
                                        {
                                            for (int j = 0; j < drow.Length; j++)
                                            {
                                                AnalyzeData(drow[j].Item1, drow[j].Item2, table.Columns[j], drow[3].Item3, report[table.Name][table.Columns[j].Name]);
                                            }
                                        }
                                        rows.Clear();
                                    }
                                    var xrow = XElement.Load(inner);
                                    var xdatas = xrow.Elements();
                                    var row = new Tuple<int, int, string>[table.Columns.Count];

                                    int i = 0;
                                    foreach (var xdata in xdatas)
                                    {
                                        if (i > table.Columns.Count)
                                        {
                                            throw new InvalidOperationException("Data file and column mismatch.");
                                        }
                                        var xmlInfo = reader as IXmlLineInfo;
                                        row[i] = new Tuple<int, int, string>(xmlInfo.LineNumber, xmlInfo.LinePosition, xdata.Value);
                                        i++;
                                    }
                                    rows.Add(row);
                                }
                            }
                        }
                    }

                    // analyize remaining rows
                    foreach (var drow in rows)
                    {
                        for (int j = 0; j < drow.Length; j++)
                        {
                            AnalyzeData(drow[j].Item1, drow[j].Item2, table.Columns[j], drow[j].Item3, report[table.Name][table.Columns[j].Name]);
                        }
                    }
                    rows.Clear();
                }
            }
        }

        void AnalyzeData(int line, int pos, Column column, string data, AnalysisReport report)
        {
            // Track max and min lengths
            report.MinLengths[0] = Math.Min(report.MinLengths[0], data.Length);
            report.MaxLengths[0] = Math.Max(report.MaxLengths[0], data.Length);

            switch (column.Type)
            {
                case DataType.NATIONAL_CHARACTER:
                case DataType.CHARACTER:
                    // Data not exactly the right length
                    if (data.Length != column.Param[0])
                    {
                        report.ReportError(line, pos, AnalysisErrorType.NOT_EXACT, data);
                    }
                    break;
                case DataType.NATIONAL_CHARACTER_VARYING:
                case DataType.CHARACTER_VARYING:
                    // Data too long
                    if (data.Length > column.Param[0])
                    {
                        report.ReportError(line, pos, AnalysisErrorType.TOO_LONG, data);
                    }
                    break;
                case DataType.DECIMAL:
                    var components = data.Split('.');

                    // No separator
                    if (components.Length == 1)
                    {
                        report.MinLengths[0] = Math.Min(report.MinLengths[0], components[0].Length);
                        report.MaxLengths[0] = Math.Max(report.MaxLengths[0], components[0].Length);
                        report.MinLengths[1] = Math.Min(report.MinLengths[1], 0);
                        report.MaxLengths[1] = Math.Max(report.MaxLengths[1], 0);

                        if (components[0].Length > column.Param[0])
                        {
                            report.ReportError(line, pos, AnalysisErrorType.TOO_LONG, data);
                        }
                    }
                    // With separator
                    else if (components.Length == 2)
                    {
                        report.MinLengths[0] = Math.Min(report.MinLengths[0], components[0].Length + components[1].Length);
                        report.MaxLengths[0] = Math.Max(report.MaxLengths[0], components[0].Length + components[1].Length);
                        report.MinLengths[1] = Math.Min(report.MinLengths[1], components[1].Length);
                        report.MaxLengths[1] = Math.Max(report.MaxLengths[1], components[1].Length);

                        if (components[0].Length + components[1].Length > column.Param[0] || components[1].Length > column.Param[1])
                        {
                            report.ReportError(line, pos, AnalysisErrorType.MISMATCH, data);
                        }
                    }

                    break;
            }
        }
    }
}
