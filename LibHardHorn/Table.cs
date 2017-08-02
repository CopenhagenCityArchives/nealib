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
    /// <summary>
    /// A table in an archive version.
    /// </summary>
    public class Table
    {
        public ArchiveVersion ArchiveVersion { get; private set; }

        public List<Column> Columns { get; private set; }

        public string Name { get; private set; }

        public string Folder { get; private set; }

        public int Rows { get; private set; }

        public string Description { get; private set; }

        /// <summary>
        /// Construct a table.
        /// </summary>
        /// <param name="name">The name of the table.</param>
        /// <param name="folder">The folder of the table.</param>
        /// <param name="rows">The number of rows in the table.</param>
        /// <param name="description">A description of the table.</param>
        /// <param name="columns">The columns of the table.</param>
        public Table(ArchiveVersion archiveVersion, string name, string folder, int rows, string description, List<Column> columns)
        {
            ArchiveVersion = archiveVersion;
            Name = name;
            Folder = folder;
            Columns = columns;
            Rows = rows;
            Description = description;
        }

        /// <summary>
        /// Parse an XML element, to get a table.
        /// </summary>
        /// <param name="archiveVersion">The archive version, the table is a part of.</param>
        /// <param name="ns">The XML namespace to use.</param>
        /// <param name="xtable">The XML element to parse.</param>
        /// <param name="log">The logger.</param>
        /// <returns></returns>
        public static Table Parse(ArchiveVersion archiveVersion, XElement xtable, ILogger log)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            string name = xtable.Element(xmlns + "name").Value;
            string folder = xtable.Element(xmlns + "folder").Value;
            int rows = int.Parse(xtable.Element(xmlns + "rows").Value);
            string desc = xtable.Element(xmlns + "description").Value;

            var table = new Table(archiveVersion, name, folder, rows, desc, new List<Column>());

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
                    (table.Columns as List<Column>).Add(new Column(table, "DUMMY" + (dummyCount++).ToString(), DataType.NOT_DEFINED, false, null, "", ""));
                }
                catch (ArchiveVersionColumnParsingException ex)
                {
                    log.Log(string.Format("En fejl opstod under afkodningen af en kolonne i tabellen '{0}': {1}", table.Name, ex.Message), LogLevel.ERROR);
                    (table.Columns as List<Column>).Add(new Column(table, "DUMMY" + (dummyCount++).ToString(), DataType.NOT_DEFINED, false, null, "", ""));
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
        public int Read(out Row[] rows, int n = 100000)
        {
            rows = new Row[n];
            int row = 0;

            while (_xmlReader.Read() && row < n)
            {
                if (_xmlReader.NodeType == XmlNodeType.Element && _xmlReader.Name.Equals("row"))
                {
                    using (XmlReader inner = _xmlReader.ReadSubtree())
                    {
                        if (inner.Read())
                        {
                            var xrow = XElement.Load(inner);
                            var xdatas = xrow.Elements();
                            rows[row] = new Row(_table.Columns.Count);
                            int col = 0;
                            foreach (var xdata in xdatas)
                            {
                                if (col > _table.Columns.Count)
                                {
                                    throw new InvalidOperationException("Data file and column mismatch.");
                                }
                                var xmlInfo = _xmlReader as IXmlLineInfo;
                                var isNull = false;
                                if (xdata.HasAttributes)
                                {
                                    var xnull = xdata.Attribute(xmlnsxsi + "nil");
                                    bool.TryParse(xnull.Value, out isNull);
                                }
                                rows[row][col] = new Post(xdata.Value, xmlInfo.LineNumber, xmlInfo.LinePosition, isNull);
                                col++;
                            }
                            row++;
                        }
                    }
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
        // ~TableReader() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class Post
    {
        public int Line { get; set; }
        public int Position { get; set; }
        public bool IsNull { get; set; }
        public string Data { get; set; }

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
        }
    }
}
