using HardHorn.Archiving;
using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace HardHorn.Utility
{

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

}
