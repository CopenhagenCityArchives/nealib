using NEA.Archiving;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace NEA.Utility
{

    public class FileIndexReader : IDisposable
    {
        Stream _stream;
        XmlReader _xmlReader;
        XNamespace xmlnsxsi = "http://www.w3.org/2001/XMLSchema-instance";
        ArchiveVersion _archiveversion;

        public FileIndexReader(ArchiveVersion archiveversion)
        {
            _archiveversion = archiveversion;
            _stream = new FileStream(Path.Combine(archiveversion.Path, "Indices", "fileIndex.xml"), FileMode.Open, FileAccess.Read);
            _xmlReader = XmlReader.Create(_stream);
        }

        /// <summary>
        /// Read rows of the table.
        /// </summary>
        /// <param name="rows"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        public int ReadN(out AVFile[] files, int n = 100000, int offset = 0)
        {
            files = new Archiving.AVFile[n];
            int row = 0;

            while (row < n && _xmlReader.Read())
            {
                if (_xmlReader.NodeType == XmlNodeType.Element && _xmlReader.Name.Equals("f"))
                {
                    using (XmlReader inner = _xmlReader.ReadSubtree())
                    {
                        if (inner.Read())
                        {
                            files[row] = new AVFile(
                                XElement.Load(inner).Element(xmlnsxsi + "foN").Value, 
                                XElement.Load(inner).Element(xmlnsxsi + "fiN").Value,
                                _archiveversion);
                        }
                    }
                    row++;
                }
            }

            return row;
        }

        public IEnumerable<AVFile> Read(int chunkSize = 100000, int offset = 0)
        {
            int rowsRead = 0;
            AVFile[] files;

            while ((rowsRead = ReadN(out files, chunkSize, offset)) > 0)
            {
                for (int i = 0; i < rowsRead; i++)
                {
                    yield return files[i];
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
        ~FileIndexReader()
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
