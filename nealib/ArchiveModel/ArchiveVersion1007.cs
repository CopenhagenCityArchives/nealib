using NEA.ArchiveModel.BKG1007;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace NEA.ArchiveModel
{
    public class ArchiveVersion1007 : BaseArchiveVersion
    {
        public archiveIndex ArchiveIndex { get; set; }
        public contextDocumentationIndex ContextDocumentationIndex { get; set; }
        public docIndexType DocIndex { get; set; }
        public fileIndexType FileIndex { get; set; }
        public siardDiark TableIndex { get; set; }
        public ArchiveVersion1007(ArchiveVersionInfo info) : base(info)
        {
        }

        protected override void Load(string folderPath)
        {
            using (var stream = File.OpenRead($"{folderPath}\\Indices\\archiveIndex.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(archiveIndex));
                ArchiveIndex = (archiveIndex)serializer.Deserialize(stream);
            }
            using (var stream = File.OpenRead($"{folderPath}\\Indices\\contextDocumentationIndex.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(contextDocumentationIndex));
                ContextDocumentationIndex = (contextDocumentationIndex)serializer.Deserialize(stream);
            }
            using (var stream = File.OpenRead($"{folderPath}\\Indices\\docIndex.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(docIndexType));
                DocIndex = (docIndexType)serializer.Deserialize(stream);
            }
            using (var stream = File.OpenRead($"{folderPath}\\Indices\\fileIndex.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(fileIndexType));
                FileIndex = (fileIndexType)serializer.Deserialize(stream);
            }
            using (var stream = File.OpenRead($"{folderPath}\\Indices\\tableIndex.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(siardDiark));
                TableIndex = (siardDiark)serializer.Deserialize(stream);
            }
        }

        public override Dictionary<string, byte[]> GetChecksumDict()
        {
            return FileIndex.f.ToDictionary(f => $"{f.foN}\\{f.fiN}", f => f.md5);
        }
    }
}
