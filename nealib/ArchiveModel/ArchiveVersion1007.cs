using log4net;
using NEA.ArchiveModel.BKG1007;
using NEA.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace NEA.ArchiveModel
{
    public class ArchiveVersion1007 : ArchiveVersion
    {
        #region public members
        private archiveIndex archiveIndex;
        private contextDocumentationIndex contextDocumentationIndex;
        private docIndexType docIndex;
        private fileIndexType fileIndex;
        private siardDiark tableIndex;

        public archiveIndex ArchiveIndex 
        { 
            get 
            {
                if (archiveIndex == null)
                {
                    LoadArchiveIndex();
                }
                return archiveIndex;
            } 
            set => archiveIndex = value; 
        }
        public contextDocumentationIndex ContextDocumentationIndex
        {
            get
            {
                if (contextDocumentationIndex == null)
                {
                    LoadContextDocumentationIndex();
                }
                return contextDocumentationIndex;
            }
            set => contextDocumentationIndex = value;
        }
        public docIndexType DocIndex
        {
            get
            {
                if (docIndex == null)
                {
                    LoadDocIndex();
                }
                return docIndex;
            }
            set => docIndex = value;
        }
        public fileIndexType FileIndex
        {
            get
            {
                if (fileIndex == null)
                {
                    LoadFileIndex();
                }
                return fileIndex;
            }
            set => fileIndex = value;
        }
        public siardDiark TableIndex
        {
            get
            {
                if (tableIndex == null)
                {
                    LoadTableIndex();
                }
                return tableIndex;
            }
            set => tableIndex = value;
        }
        #endregion

        private readonly string _indexFolderPath;
        private readonly string _fileIndexPath;

        public ArchiveVersion1007(ArchiveVersionInfo info, IFileSystem fileSystem = null) : base(info, fileSystem)
        {
            _indexFolderPath = $"{Info.Medias[Info.Id + ".1"]}\\Indices";
            _fileIndexPath = _indexFolderPath + "\\fileIndex.xml";
        }
        #region Load
        public override void Load()
        {
            LoadArchiveIndex();
            LoadContextDocumentationIndex();
            LoadDocIndex();
            LoadFileIndex();
            LoadTableIndex();
        }
        public void LoadArchiveIndex()
        {
            using (var stream = _fileSystem.File.OpenRead(_indexFolderPath+ "\\archiveIndex.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(archiveIndex));
                ArchiveIndex = (archiveIndex)serializer.Deserialize(stream);
            }
        }
        public void LoadContextDocumentationIndex()
        {
            using (var stream = _fileSystem.File.OpenRead(_indexFolderPath + "\\contextDocumentationIndex.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(contextDocumentationIndex));
                ContextDocumentationIndex = (contextDocumentationIndex)serializer.Deserialize(stream);
            }
        }
        public void LoadDocIndex()
        {
            using (var stream = _fileSystem.File.OpenRead(_indexFolderPath + "\\docIndex.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(docIndexType));
                DocIndex = (docIndexType)serializer.Deserialize(stream);
            }
        }
        public void LoadFileIndex()
        {
            using (var stream = _fileSystem.File.OpenRead(FileIndexPath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(fileIndexType));
                FileIndex = (fileIndexType)serializer.Deserialize(stream);
            }
        }
        public void LoadTableIndex()
        {
            using (var stream = _fileSystem.File.OpenRead(_indexFolderPath + "\\tableIndex.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(siardDiark));
                TableIndex = (siardDiark)serializer.Deserialize(stream);
            }
        }
        #endregion
        public override Dictionary<string, byte[]> GetChecksumDict()
        {
            //If the fileindex has allready been loaded into memory we get it from there
            if (fileIndex != null)
            {
                return FileIndex.f.ToDictionary(f => $"{f.foN}\\{f.fiN}", f => f.md5);
            }
            //Otherwise we stream it in from the xml to keep down memory usage
            using (var stream = _fileSystem.FileStream.Create(FileIndexPath, FileMode.Open))
            {
                var fileindex = XDocument.Load(stream);
                var ns = fileindex.Root.Name.Namespace;
                return fileindex.Descendants(ns.GetName("f"))
                    .ToDictionary(f => $"{f.Element(ns.GetName("foN")).Value}\\{f.Element(ns.GetName("fiN")).Value}", f => ByteHelper.ParseHex(f.Element(ns.GetName("md5")).Value));
            }
        }
        public override TableReader GetTableReader(string tableName, string mediaId)
        {
            return new TableReader1007(TableIndex.tables.FirstOrDefault(x => x.name == tableName), this, mediaId, _fileSystem);
        }

        protected override string GetFileIndexPath()
        {
            return _fileIndexPath;
        }
    }
}
