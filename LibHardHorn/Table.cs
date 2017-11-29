using HardHorn.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.IO;
using System.Xml;

namespace HardHorn.Archiving
{
    public class PrimaryKey
    {
        public string Name { get; private set; }
        public IEnumerable<string> Columns { get; private set; }

        public PrimaryKey(string name, IEnumerable<string> columns)
        {
            Name = name;
            Columns = columns;
        }

        public static PrimaryKey Parse(XElement element)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            var name = element.Element(xmlns + "name").Value;
            var columns = element.Elements(xmlns + "column").Select(e => e.Value);

            return new PrimaryKey(name, columns);
        }

        public XElement ToXml()
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            return new XElement(xmlns + "primaryKey",
                new XElement(xmlns + "name", Name),
                Columns.Select(c => new XElement(xmlns + "column", c)));
        }
    }

    public class ForeignKey
    {
        public class Reference
        {
            public string Column { get; private set; }
            public string Referenced { get; private set; }

            public Reference(string column, string referenced)
            {
                Column = column;
                Referenced = referenced;
            }

            public static Reference Parse(XElement element)
            {
                XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

                var column = element.Element(xmlns + "column").Value;
                var referenced = element.Element(xmlns + "referenced").Value;

                return new Reference(column, referenced);
            }

            public XElement ToXml()
            {
                XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

                return new XElement(xmlns + "reference",
                    new XElement(xmlns + "column", Column),
                    new XElement(xmlns + "referenced", Referenced));
            }
        }

        public string Name { get; private set; }
        public string ReferencedTable { get; private set; }
        public List<Reference> References { get; private set; }

        public ForeignKey(string name, string referencedTable, IEnumerable<Reference> references)
        {
            Name = name;
            ReferencedTable = referencedTable;
            References = new List<Reference>(references);
        }

        public static ForeignKey Parse(XElement element)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            var name = element.Element(xmlns + "name").Value;
            var referencedTable = element.Element(xmlns + "referencedTable").Value;
            var references = element.Elements(xmlns + "reference").Select(Reference.Parse);

            return new ForeignKey(name, referencedTable, references);
        }

        public XElement ToXml()
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            return new XElement(xmlns + "foreignKey",
                new XElement(xmlns + "name", Name),
                new XElement(xmlns + "referencedTable", ReferencedTable),
                References.Select(r => r.ToXml()));
        }
    }

    /// <summary>
    /// A table in an archive version.
    /// </summary>
    public class Table
    {
        public ArchiveVersion ArchiveVersion { get; set; }

        public List<Column> Columns { get; private set; }

        public string Name { get; private set; }

        public string Folder { get; private set; }

        public int Rows { get; private set; }

        public string Description { get; private set; }

        public PrimaryKey PrimaryKey { get; private set; }
        public List<ForeignKey> ForeignKeys {get;private set;}

        /// <summary>
        /// Construct a table.
        /// </summary>
        /// <param name="name">The name of the table.</param>
        /// <param name="folder">The folder of the table.</param>
        /// <param name="rows">The number of rows in the table.</param>
        /// <param name="description">A description of the table.</param>
        /// <param name="columns">The columns of the table.</param>
        public Table(string name, string folder, int rows, string description, List<Column> columns, PrimaryKey primaryKey, IEnumerable<ForeignKey> foreignKeys)
        {
            Name = name;
            Folder = folder;
            Columns = columns;
            Rows = rows;
            Description = description;
            PrimaryKey = primaryKey;
            ForeignKeys = new List<ForeignKey>(foreignKeys);
        }

        /// <summary>
        /// Parse an XML element, to get a table.
        /// </summary>
        /// <param name="archiveVersion">The archive version, the table is a part of.</param>
        /// <param name="ns">The XML namespace to use.</param>
        /// <param name="xtable">The XML element to parse.</param>
        /// <param name="log">The logger.</param>
        /// <returns></returns>
        public static Table Parse(XElement xtable, ILogger log, Action<Exception> callback = null)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            string name = xtable.Element(xmlns + "name").Value;
            string folder = xtable.Element(xmlns + "folder").Value;
            int rows = int.Parse(xtable.Element(xmlns + "rows").Value);
            string desc = xtable.Element(xmlns + "description").Value;
            var pkey = PrimaryKey.Parse(xtable.Element(xmlns + "primaryKey"));
            var xfkeys = xtable.Element(xmlns + "foreignKeys");
            var fkeys = Enumerable.Empty<ForeignKey>();
            if (xfkeys != null)
            {
                fkeys = xfkeys.Elements().Select(xfkey => ForeignKey.Parse(xfkey));
            }

            var table = new Table(name, folder, rows, desc, new List<Column>(), pkey, fkeys);

            int dummyCount = 1;
            var xcolumns = xtable.Element(xmlns + "columns");
            foreach (var xcolumn in xcolumns.Elements(xmlns + "column"))
            {
                try
                {
                    var column = Column.Parse(table, xcolumn);
                    (table.Columns as List<Column>).Add(column);
                }
                catch (ArchiveVersionColumnTypeParsingException ex)
                {
                    log.Log(string.Format("En fejl opstod under afkodningen af kolonnen '{0}' i tabellen '{1}': Typen '{2}' er ikke valid.", ex.Name, table.Name, ex.Type), LogLevel.ERROR);
                    (table.Columns as List<Column>).Add(new Column(table, ex.Name, DataType.UNDEFINED, null, false, null, "", ex.Id, int.Parse(ex.Id.Substring(1))));
                    if (callback != null)
                        callback(ex);
                }
                catch (ArchiveVersionColumnParsingException ex)
                {
                    log.Log(string.Format("En fejl opstod under afkodningen af en kolonne i tabellen '{0}': {1}", table.Name, ex.Message), LogLevel.ERROR);
                    (table.Columns as List<Column>).Add(new Column(table, "__Ugyldig_Kolonne" + (dummyCount++).ToString() + "__", DataType.UNDEFINED, null, false, null, "", "", 0));
                    if (callback != null)
                        callback(ex);
                }
            }

            return table;
        }

        /// <summary>
        /// Get a reader.
        /// </summary>
        /// <returns>The reader.</returns>
        public TableReader GetReader()
        {
            return new TableReader(this);
        }

        public TableComparison CompareTo(Table oldTable)
        {
            var tableComparison = new TableComparison(this, oldTable);
            tableComparison.Name = Name;

            if (Rows != oldTable.Rows)
            {
                tableComparison.Modified = true;
                tableComparison.RowsModified = true;
            }

            if (Description != oldTable.Description)
            {
                tableComparison.Modified = true;
                tableComparison.DescriptionModified = true;
            }

            if (Folder != oldTable.Folder)
            {
                tableComparison.Modified = true;
                tableComparison.FolderModified = true;
            }

            foreach (var column in Columns)
            {
                bool columnAdded = true;
                foreach (var oldColumn in oldTable.Columns)
                {
                    if (column.Name.ToLower() == oldColumn.Name.ToLower())
                    {
                        var columnComparison = column.CompareTo(oldColumn);
                        columnComparison.Name = column.Name;
                        tableComparison.Columns.Add(columnComparison);
                        columnAdded = false;
                        break;
                    }
                }

                if (columnAdded)
                {
                    tableComparison.Columns.Add(new ColumnComparison(column, null) { Name = column.Name, Added = true });
                }
            }

            foreach (var oldColumn in oldTable.Columns)
            {
                bool columnRemoved = true;
                foreach (var column in Columns)
                {
                    if (column.Name.ToLower() == oldColumn.Name.ToLower())
                    {
                        columnRemoved = false;
                    }
                }

                if (columnRemoved)
                {
                    tableComparison.Columns.Add(new ColumnComparison(null, oldColumn) { Name = oldColumn.Name, Removed = true });
                }
            }

            foreach (dynamic col in tableComparison.Columns)
            {
                tableComparison.Modified = col.Modified || tableComparison.Modified;
                tableComparison.ColumnsModified = col.Modified || tableComparison.ColumnsModified;
            }

            return tableComparison;
        }

        internal XElement ToXml()
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            return new XElement(xmlns + "table",
                new XElement(xmlns + "name", Name),
                new XElement(xmlns + "folder", Folder),
                new XElement(xmlns + "description", Description),
                new XElement(xmlns + "columns", Columns.Select(c => c.ToXml())),
                PrimaryKey.ToXml(),
                new XElement(xmlns + "foreignKeys", ForeignKeys.Select(fKey => fKey.ToXml())),
                new XElement(xmlns + "rows", Rows));
        }
    }

    public class TableComparison
    {
        public bool Added { get; set; }
        public List<ColumnComparison> Columns { get; set; }
        public bool ColumnsModified { get; set; }
        public bool DescriptionModified { get; set; }
        public bool FolderModified { get; set; }
        public bool Modified { get; set; }
        public string Name { get; set; }
        public Table NewTable { get; set; }
        public Table OldTable { get; set; }
        public bool Removed { get; set; }
        public bool RowsModified { get; set; }

        public TableComparison(Table newTable, Table oldTable)
        {
            NewTable = newTable;
            OldTable = oldTable;
            Columns = new List<ColumnComparison>();
            Added = false;
            Modified = false;
            Removed = false;
            DescriptionModified = false;
            ColumnsModified = false;
            RowsModified = false;
            FolderModified = false;
        }
    }

    public class TableReader : IDisposable
    {
        FileStream _fileStream;
        XmlReader _xmlReader;
        Table _table;
        XNamespace xmlnsxsi = "http://www.w3.org/2001/XMLSchema-instance";

        public TableReader(Table table)
        {
            _table = table;
            _fileStream = new FileStream(Path.Combine(table.ArchiveVersion.Path, "Tables", table.Folder, table.Folder + ".xml"), FileMode.Open, FileAccess.Read);
            _xmlReader = XmlReader.Create(_fileStream);
        }

        /// <summary>
        /// Read rows of the table.
        /// </summary>
        /// <param name="rows"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public int Read(out Post[,] rows, int n = 100000)
        {
            rows = new Post[n, _table.Columns.Count];
            int row = 0;

            while (_xmlReader.Read() && row < n)
            {
                if (_xmlReader.NodeType == XmlNodeType.Element && _xmlReader.Name.Equals("row"))
                {
                    using (XmlReader inner = _xmlReader.ReadSubtree())
                    {
                        if (inner.Read())
                        {
                            int col = 0;
                            foreach (var xpost in XElement.Load(inner).Elements())
                            {
                                if (col > _table.Columns.Count)
                                {
                                    throw new InvalidOperationException("Data file and column mismatch.");
                                }
                                var xmlInfo = _xmlReader as IXmlLineInfo;
                                var isNull = false;
                                if (xpost.HasAttributes)
                                {
                                    var xnull = xpost.Attribute(xmlnsxsi + "nil");
                                    bool.TryParse(xnull.Value, out isNull);
                                }
                                rows[row, col] = new Post();
                                rows[row, col].Data = xpost.Value;
                                rows[row, col].Line = xmlInfo.LineNumber;
                                rows[row, col].Position = xmlInfo.LinePosition;
                                rows[row, col].IsNull = isNull;
                                col++;
                            }
                        }
                    }
                    row++;
                }
            }

            return row;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _xmlReader.Close();
                    _xmlReader.Dispose();
                    _fileStream.Close();
                    _fileStream.Dispose();
                    _table = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~TableReader()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class Post
    {
        public int Line { get; set; }
        public int Position { get; set; }
        public bool IsNull { get; set; }
        public string Data { get; set; }

        public Post()
        { }

        public Post(string data, int line, int position, bool isNull)
        {
            Line = line;
            Position = position;
            IsNull = isNull;
            Data = data;
        }
    }

    public class Row
    {
        public int FieldCount { get; private set; }
        Post[] _fields;

        public Post this[int index]
        {
            get { return _fields[index]; }
            set { _fields[index] = value; }
        }

        public Row(int count)
        {
            FieldCount = count;
            _fields = new Post[count];
            for (int i = 0; i < count; i++)
            {
                _fields[i] = new Post();
            }
        }
    }
}
