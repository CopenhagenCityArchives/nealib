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

        public IEnumerable<Tuple<int, int, string>> Instances { get { return _instances as IEnumerable<Tuple<int, int, string>>; } }
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
        public Tuple<DataType, DataTypeParam> SuggestedType { get; set; }
        public bool AnalysisStarted { get; set; }

        public AnalysisReport(Column column)
        {
            AnalysisStarted = false;
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
                    else if (MaxLengths[0] > Column.Param[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.CHARACTER_VARYING, new DataTypeParam(MaxLengths[0]));
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
        public HashSet<AnalysisErrorType> Tests { get; set; }

        string _location;
        XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";
        XNamespace xmlnsxsi = "http://www.w3.org/2001/XMLSchema-instance";

        Table currentTable;
        FileStream tableStream;
        XmlReader tableReader;

        public Dictionary<string, Dictionary<string, AnalysisReport>> Report { get; private set; }

        public DataAnalyzer(string location)
        {
            Tests = new HashSet<AnalysisErrorType>();
            _location = location;
            var tableIndexDocument = XDocument.Load(Path.Combine(_location, "Indices", "tableIndex.xml"));

            Report = new Dictionary<string, Dictionary<string, AnalysisReport>>();

            Tables = new List<Table>();

            var xtables = tableIndexDocument.Descendants(xmlns + "tables").First();
            foreach (var xtable in xtables.Elements(xmlns + "table"))
            {
                Table table;
                if (Table.TryParse(xmlns, xtable, out table))
                {
                    (Tables as List<Table>).Add(table);
                }
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
            var rows = new Tuple<int, int, bool, string>[currentTable.Columns.Count, n];
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
                                rows[i, row] = new Tuple<int, int, bool, string>(xmlInfo.LineNumber, xmlInfo.LinePosition, isNull, xdata.Value);
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
                    AnalyzeData(rows[j, i].Item1, 
                        rows[j, i].Item2,
                        currentTable.Columns[j],
                        rows[j, i].Item4,
                        rows[j, i].Item3, 
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

        void AnalyzeData(int line, int pos, Column column, string data, bool isNull, AnalysisReport report)
        {
            switch (column.Type)
            {
                case DataType.NATIONAL_CHARACTER:
                case DataType.CHARACTER:
                    if (report.AnalysisStarted)
                    {
                        report.MinLengths[0] = Math.Min(report.MinLengths[0], data.Length);
                        report.MaxLengths[0] = Math.Max(report.MaxLengths[0], data.Length);
                    }
                    else
                    {
                        report.MinLengths[0] = data.Length;
                        report.MaxLengths[0] = data.Length;
                    }

                    // Data not exactly the right length
                    if (Tests.Contains(AnalysisErrorType.NOT_EXACT) && data.Length != column.Param[0])
                    {
                        report.ReportError(line, pos, AnalysisErrorType.NOT_EXACT, data);
                    }
                    break;
                case DataType.NATIONAL_CHARACTER_VARYING:
                case DataType.CHARACTER_VARYING:
                    if (report.AnalysisStarted)
                    {
                        report.MinLengths[0] = Math.Min(report.MinLengths[0], data.Length);
                        report.MaxLengths[0] = Math.Max(report.MaxLengths[0], data.Length);
                    }
                    else
                    {
                        report.MinLengths[0] = data.Length;
                        report.MaxLengths[0] = data.Length;
                    }

                    // Data too long
                    if (Tests.Contains(AnalysisErrorType.TOO_LONG) && data.Length > column.Param[0])
                    {
                        report.ReportError(line, pos, AnalysisErrorType.TOO_LONG, data);
                    }
                    break;
                case DataType.DECIMAL:
                    var components = data.Split('.');
                    if (report.AnalysisStarted)
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

                    // No separator
                    if (Tests.Contains(AnalysisErrorType.TOO_LONG) && components.Length == 1)
                    {
                        if (Tests.Contains(AnalysisErrorType.TOO_LONG) && components[0].Length > column.Param[0])
                        {
                            report.ReportError(line, pos, AnalysisErrorType.TOO_LONG, data);
                        }
                    }
                    // With separator
                    else if (Tests.Contains(AnalysisErrorType.MISMATCH) && components.Length == 2)
                    {
                        if (components[0].Length + components[1].Length > column.Param[0] || components[1].Length > column.Param[1])
                        {
                            report.ReportError(line, pos, AnalysisErrorType.MISMATCH, data);
                        }
                    }
                    break;
            }

            if (Tests.Contains(AnalysisErrorType.NOT_NULLABLE) && isNull && !column.Nullable)
            {
                report.ReportError(line, pos, AnalysisErrorType.NOT_NULLABLE, data);
            }

            if (Tests.Contains(AnalysisErrorType.NULL_NOT_EMPTY) && isNull && data.Length > 0)
            {
                report.ReportError(line, pos, AnalysisErrorType.NULL_NOT_EMPTY, data);
            }

            report.AnalysisStarted = true;
        }
    }
}
