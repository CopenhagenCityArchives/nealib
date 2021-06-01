using NEA.ArchiveModel.BKG1007;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NEA.ArchiveModel
{
    class TableReader1007 : TableReader
    {
        private readonly Stream _stream;
        private readonly XmlReader _xmlReader;
        private readonly tableType _table;
        private readonly XNamespace xmlnsxsi = "http://www.w3.org/2001/XMLSchema-instance";
        private readonly IFileSystem _fileSystem;

        public TableReader1007(tableType table, ArchiveVersion av, string mediaId,IFileSystem fileSystem)
        {
            _table = table;
            _fileSystem = fileSystem;
            _stream = _fileSystem.File.OpenRead(table.GetTableRowsPath(av.Info.Medias[mediaId]));
            _xmlReader = XmlReader.Create(_stream);            
        }

        /// <summary>
        /// Read rows of the table.
        /// </summary>
        /// <param name="rows"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public override int ReadN(out Post[,] rows, int n = 100000, int offset = 0, bool readRaw = false)
        {
            rows = new Post[n, _table.columns.Length];
            int row = 0;
            string data;

            while (row < n && _xmlReader.Read())
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
                                if (col > _table.columns.Length)
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

                                if (readRaw)
                                {
                                    System.Text.StringBuilder raw = new System.Text.StringBuilder();
                                    foreach (var node in xpost.Nodes())
                                    {
                                        raw.Append(node.ToString());
                                    }
                                    data = raw.ToString();
                                }
                                else
                                {
                                    data = xpost.Value;
                                }

                                rows[row, col] = new Post(data, isNull, xmlInfo.LineNumber, xmlInfo.LinePosition, row + offset);
                                col++;
                            }
                        }
                    }
                    row++;
                }
            }

            return row;
        }

        public override IEnumerable<Post[]> Read(int chunkSize = 100000, int offset = 0, bool readRaw = false)
        {
            int rowsRead = 0;
            Post[,] rows;

            while ((rowsRead = ReadN(out rows, chunkSize, offset, readRaw)) > 0)
            {
                for (int i = 0; i < rowsRead; i++)
                {
                    Post[] row = new Post[_table.columns.Length];
                    for (int j = 0; j < _table.columns.Length; j++)
                    {
                        row[j] = rows[i, j];
                    }
                    yield return row;
                }
            }
            yield break;
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
                    _stream.Close();
                    _stream.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~TableReader1007()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public override void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
