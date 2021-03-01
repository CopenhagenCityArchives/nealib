﻿using NEA.Archiving;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace NEA.Utility
{

    public class FileIndexReader
    {
        FileInfo FilePath;

        public FileIndexReader(ArchiveVersion archiveversion)
        {
            FilePath = new FileInfo(Path.Combine(archiveversion.Path, archiveversion.Medias[0], "Indices", "fileIndex.xml"));
            
            if (!FilePath.Exists)
            {
                throw new Exception(String.Format("Could not find fileIndex.xml in this path", FilePath));
            }
        }

        /// <summary>
        /// Read files from fileIndex.xml
        /// </summary>
        /// <returns>IEnumerable<AVFile></returns>
        public IEnumerable<AVFile> ReadFiles()
        {
            XDocument fileIndex = XDocument.Load(FilePath.FullName);
            var ns = fileIndex.Root.Name.Namespace;

            return from f in fileIndex.Descendants(ns.GetName("f"))
                   select new AVFile(f.Element(ns.GetName("foN")).Value, f.Element(ns.GetName("fiN")).Value, f.Element(ns.GetName("md5")).Value);
        }
    }

}
