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
        public ArchiveVersion1007(ArchiveVersionInfo info, IFileSystem fileSystem = null) : base(info, fileSystem)
        {
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
            using (var stream = _fileSystem.File.OpenRead($"{Info.FolderPath}\\Indices\\archiveIndex.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(archiveIndex));
                ArchiveIndex = (archiveIndex)serializer.Deserialize(stream);
            }
        }
        public void LoadContextDocumentationIndex()
        {
            using (var stream = _fileSystem.File.OpenRead($"{Info.FolderPath}\\Indices\\contextDocumentationIndex.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(contextDocumentationIndex));
                ContextDocumentationIndex = (contextDocumentationIndex)serializer.Deserialize(stream);
            }
        }
        public void LoadDocIndex()
        {
            using (var stream = _fileSystem.File.OpenRead($"{Info.FolderPath}\\Indices\\docIndex.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(docIndexType));
                DocIndex = (docIndexType)serializer.Deserialize(stream);
            }
        }
        public void LoadFileIndex()
        {
            using (var stream = _fileSystem.File.OpenRead($"{Info.FolderPath}\\Indices\\fileIndex.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(fileIndexType));
                FileIndex = (fileIndexType)serializer.Deserialize(stream);
            }
        }
        public void LoadTableIndex()
        {
            using (var stream = _fileSystem.File.OpenRead($"{Info.FolderPath}\\Indices\\tableIndex.xml"))
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
            using (var stream = _fileSystem.FileStream.Create($"{Info.Medias[0]}\\Indices\\fileIndex.xml", FileMode.Open))
            {
                var fileindex = XDocument.Load(stream);
                var ns = fileindex.Root.Name.Namespace;
                return fileindex.Descendants(ns.GetName("f"))
                    .ToDictionary(f => $"{f.Element(ns.GetName("foN")).Value}\\{f.Element(ns.GetName("fiN")).Value}", f => ByteHelper.ParseHex(f.Element(ns.GetName("md5")).Value));
            }
        }
        public override TableReader GetTableReader(string tableName)
        {
            return new TableReader1007(TableIndex.tables.FirstOrDefault(x => x.name == tableName), this, _fileSystem);
        }

        public override GetFilesResult GetFiles()
        {
            var metadata = Enumerable.Empty<string>();
            var tables = Enumerable.Empty<string>();
            var documents = Enumerable.Empty<string>();
            if (Info.Medias.Any())
            {
                foreach (var media in Info.Medias)
                {
                    metadata = metadata.Concat(_fileSystem.Directory.GetFiles(media));
                    metadata = metadata.Concat(_fileSystem.Directory.GetFiles($"{media}\\Indices", "*", System.IO.SearchOption.AllDirectories));
                    metadata = metadata.Concat(_fileSystem.Directory.GetFiles($"{media}\\Schemas", "*", System.IO.SearchOption.AllDirectories));
                    metadata = metadata.Concat(_fileSystem.Directory.GetFiles($"{media}\\ContextDocumentation", "*", System.IO.SearchOption.AllDirectories));
                    tables = tables.Concat(_fileSystem.Directory.GetFiles($"{media}\\Tables", "*", System.IO.SearchOption.AllDirectories));
                    documents = documents.Concat(_fileSystem.Directory.GetFiles($"{media}\\Documents", "*", System.IO.SearchOption.AllDirectories));
                }
            }
            else
            {
                metadata = metadata.Concat(_fileSystem.Directory.GetFiles(Info.FolderPath).AsEnumerable());
                metadata = metadata.Concat(_fileSystem.Directory.GetFiles($"{Info.FolderPath}\\Indices", "*", System.IO.SearchOption.AllDirectories));
                metadata = metadata.Concat(_fileSystem.Directory.GetFiles($"{Info.FolderPath}\\Schemas", "*", System.IO.SearchOption.AllDirectories));
                metadata = metadata.Concat(_fileSystem.Directory.GetFiles($"{Info.FolderPath}\\ContextDocumentation", "*", System.IO.SearchOption.AllDirectories));
                tables = tables.Concat(_fileSystem.Directory.GetFiles($"{Info.FolderPath}\\Tables", "*", System.IO.SearchOption.AllDirectories));
                documents = documents.Concat(_fileSystem.Directory.GetFiles($"{Info.FolderPath}\\Documents", "*", System.IO.SearchOption.AllDirectories));
            }     
            return new GetFilesResult(metadata.ToArray(), tables.ToArray(), documents.ToArray());
        }
    }
}
