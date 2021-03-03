﻿using log4net;
using NEA.ArchiveModel.BKG1007;
using NEA.Helpers;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
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
            var result = FileIndex.f.ToDictionary(f => $"{f.foN}\\{f.fiN}", f => f.md5);
            
            return result;
        }

        public override Dictionary<string, bool> VerifyAllChecksums(bool skipDocuments = false)
        {
            var files = _fileSystem.Directory.GetFiles(Info.FolderPath).AsEnumerable();
            files = files.Concat(_fileSystem.Directory.GetFiles($"{Info.FolderPath}\\Indices", "*", System.IO.SearchOption.AllDirectories));
            files = files.Concat(_fileSystem.Directory.GetFiles($"{Info.FolderPath}\\Schemas", "*", System.IO.SearchOption.AllDirectories));
            files = files.Concat(_fileSystem.Directory.GetFiles($"{Info.FolderPath}\\Tables", "*", System.IO.SearchOption.AllDirectories));
            if (!skipDocuments)
            {
                files = files.Concat(_fileSystem.Directory.GetFiles($"{Info.FolderPath}\\Documents", "*", System.IO.SearchOption.AllDirectories));
                files = files.Concat(_fileSystem.Directory.GetFiles($"{Info.FolderPath}\\ContextDocumentation", "*", System.IO.SearchOption.AllDirectories));
            }
            var result = new Dictionary<string, bool>();
            Parallel.ForEach(files,new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount} ,file => { 
                result.Add(file,VerifyChecksum(file));
            });
            return result;
        }
        public override bool VerifyChecksum(string filePath)
        {
            if (filePath.ToLower() == $"{Info.FolderPath}\\Indices\\fileIndex.xml".ToLower())
            {
                //Since fileindex cant contain its own reference checksum we just have to assume that its ok.
                return true;
            }
            var expected = fileIndex.f.FirstOrDefault(x => $"{x.foN}\\{x.fiN}".ToLower() == GetRelativeFilePath(filePath).ToLower()).md5;
            var md5Helper = new MD5Helper(_fileSystem);
            return expected == md5Helper.CalculateChecksum(filePath);
        }
    }
}
