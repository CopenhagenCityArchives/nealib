using NEA.Helpers;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace NEA.Testing
{
    public class Archive1007Factory : IArchiveFactory
    {
        private readonly ArchiveFileSystemFactory _parrent;
        private readonly MockFileSystem _fileSystem;
        private readonly string _basePath;
        private readonly string _name;
        private readonly RandomBufferGenerator _byteGenerator = new RandomBufferGenerator((int)ByteHelper.MegaByte*10);
        private int _tableCount;
        private int _docCollectionCount;
        private XmlWriter _fileIndexWriter;
        public Archive1007Factory(ArchiveFileSystemFactory parrent, MockFileSystem fileSystem, string basePath, string name)
        {
            _parrent = parrent;
            _fileSystem = fileSystem;
            _basePath = basePath;
            _name = name;
            InitIndexWriter();
            _fileSystem.AddDirectory($"{_basePath}\\{_name}\\{_name}.1");
            AddIndices();
            AddSchemas();
            _fileSystem.AddDirectory($"{_basePath}\\{_name}\\{_name}.1\\Tables");
            _fileSystem.AddDirectory($"{_basePath}\\{_name}\\{_name}.1\\Documents");
            _fileSystem.AddDirectory($"{_basePath}\\{_name}\\{_name}.1\\ContextDocumentation");
            _tableCount = 0;
            _docCollectionCount = 0;

        }

        private void InitIndexWriter()
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = Encoding.UTF8;
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Indices\\fileIndex.xml", new MockFileData("", Encoding.UTF8));
            _fileIndexWriter = XmlWriter.Create(_fileSystem.FileStream.Create($"{_basePath}\\{_name}\\{_name}.1\\Indices\\fileIndex.xml", System.IO.FileMode.Create), settings);
            _fileIndexWriter.WriteStartDocument();
            _fileIndexWriter.WriteStartElement("fileIndex", "http://www.sa.dk/xmlns/diark/1.0");
            _fileIndexWriter.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        }

        private void AddIndices()
        {
            var data = new MockFileData(_byteGenerator.GenerateBufferFromSeed((int)ByteHelper.MegaByte));
            var dataChecksum = GetChecksum(data);

            _fileSystem.AddDirectory($"{_basePath}\\{_name}\\{_name}.1\\Indices");
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Indices\\archiveIndex.xml", data);
            AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Indices\\archiveIndex.xml", dataChecksum);
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Indices\\contextDocumentationIndex.xml", data);
            AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Indices\\contextDocumentationIndex.xml", dataChecksum);
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Indices\\docIndex.xml", data);
            AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Indices\\docIndex.xml", dataChecksum);
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Indices\\tableIndex.xml", data);
            AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Indices\\tableIndex.xml", dataChecksum);
        }
        private void AddSchemas()
        {
            var data = new MockFileData(_byteGenerator.GenerateBufferFromSeed((int)ByteHelper.MegaByte));
            var dataChecksum = GetChecksum(data);
            _fileSystem.AddDirectory($"{_basePath}\\{_name}\\{_name}.1\\Schemas");
            _fileSystem.AddDirectory($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard");
            _fileSystem.AddDirectory($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\localShared");
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\archiveIndex.xsd", data);
            AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\archiveIndex.xsd", dataChecksum);
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\contextDocumentationIndex.xsd", data);
            AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\contextDocumentationIndex.xsd", dataChecksum);
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\docIndex.xsd", data);
            AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\docIndex.xsd", dataChecksum);
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\fileIndex.xsd", data);
            AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\fileIndex.xsd", dataChecksum);
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\researchIndex.xsd", data);
            AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\researchIndex.xsd", dataChecksum);
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\tableIndex.xsd", data);
            AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\tableIndex.xsd", dataChecksum);
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\XMLSchema.xsd", data);
            AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Schemas\\standard\\XMLSchema.xsd", dataChecksum);
        }
        public IArchiveFactory AddTables(int amount, int fileSize = (int)ByteHelper.MegaByte)
        {
            for (int i = 0; i <= amount; i++)
            {
                AddTable(fileSize);
            }
            return this;
        }
        public IArchiveFactory AddTable(int fileSize = (int)ByteHelper.MegaByte)
        {
            var data = new MockFileData(_byteGenerator.GenerateBufferFromSeed(fileSize));
            var dataChecksum = GetChecksum(data);
            var tableName = $"table{_tableCount + 1}";
            _fileSystem.AddDirectory($"{_basePath}\\{_name}\\{_name}.1\\Tables\\{tableName}");
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Tables\\{tableName}\\{tableName}.xsd", data);
            AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Tables\\{tableName}\\{tableName}.xsd", dataChecksum);
            _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Tables\\{tableName}\\{tableName}.xml", data);
            AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Tables\\{tableName}\\{tableName}.xml", dataChecksum);
            _tableCount++;
            return this;
        }
        public IArchiveFactory AddDocCollection(int numberOfFiles, int fileSize = (int)ByteHelper.MegaByte)
        {
            var collectionName = $"doc{_docCollectionCount + 1}";
            _fileSystem.AddDirectory($"{_basePath}\\{_name}\\{_name}.1\\Documents\\{collectionName}");
            var data = new MockFileData(_byteGenerator.GenerateBufferFromSeed(fileSize));
            var dataChecksum = GetChecksum(data);
            for (int i = 0; i <= numberOfFiles; i++)
            {
                _fileSystem.AddFile($"{_basePath}\\{_name}\\{_name}.1\\Documents\\{collectionName}\\document-{i}.tif", data);
                AddToFileindex($"{_basePath}\\{_name}\\{_name}.1\\Documents\\{collectionName}\\document-{i}.tif", dataChecksum);
            }
            _docCollectionCount++;
            return this;
        }
        public IFileSystem Done()
        {
            _fileIndexWriter.WriteEndDocument();
            _fileIndexWriter.Close();
            return _fileSystem;
        }

        private string GetChecksum(MockFileData data)
        {
            using (var md5 = MD5.Create())
            {
                var hash = BitConverter.ToString(md5.ComputeHash(data.Contents)).Replace("-", "");
                return hash;
            }
        }
        private void AddToFileindex(string filePath, string Checksum)
        {
            var endIndex = filePath.IndexOf(_name);
            var relativepath = filePath.Remove(0, endIndex).TrimStart('\\'); ;


            _fileIndexWriter.WriteStartElement("f");
            _fileIndexWriter.WriteStartElement("foN");
            _fileIndexWriter.WriteString(_fileSystem.Path.GetDirectoryName(relativepath));
            _fileIndexWriter.WriteEndElement();

            _fileIndexWriter.WriteStartElement("fiN");
            _fileIndexWriter.WriteString(_fileSystem.Path.GetFileName(relativepath));
            _fileIndexWriter.WriteEndElement();

            _fileIndexWriter.WriteStartElement("md5");
            _fileIndexWriter.WriteString(Checksum);
            _fileIndexWriter.WriteEndElement();
            _fileIndexWriter.WriteEndElement();
        }
    }

}
