using HardHorn.Archiving;
using HardHorn.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace HardHorn.Archiving
{
    public class TableIndex
    {
        public string Version { get; private set; }
        public string DBName { get; private set; }
        public string DatabaseProduct { get; private set; }

        public List<Table> Tables { get; private set; }
        public List<View> Views { get; private set; }

        public TableIndex(string version, string dbName, string databaseProduct, IEnumerable<Table> tables, IEnumerable<View> views)
        {
            Version = version;
            DBName = dbName;
            DatabaseProduct = databaseProduct;

            Tables = new List<Table>(tables);
            Views = new List<View>(views);
        }

        public XDocument ToXml()
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
            XNamespace xsd = "http://www.w3.org/2001/XMLSchema";

            var root = new XElement(xmlns + "siardDiark",
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                new XAttribute("xmlns", "http://www.sa.dk/xmlns/diark/1.0"),
                new XAttribute(xsi + "schemaLocation", "http://www.sa.dk/xmlns/diark/1.0 ../Schemas/standard/tableIndex.xsd"),
                new XElement(xmlns + "version", Version),
                new XElement(xmlns + "dbName", DBName),
                new XElement(xmlns + "databaseProduct", DatabaseProduct),
                new XElement(xmlns + "tables", Tables.Select(t => t.ToXml())),
                new XElement(xmlns + "views", Views.Select(v => v.ToXml())));

            return new XDocument(root);
        }

        public static TableIndex Parse(XElement element, ILogger logger, Action<Exception> callback = null)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            var version = element.Element(xmlns + "version").Value;
            var dbName = element.Element(xmlns + "dbName").Value;
            var databaseProduct = element.Element(xmlns + "databaseProduct").Value;

            var tables = element.Element(xmlns + "tables").Elements().Select(xtable => Table.Parse(xtable, logger, callback));

            var xviews = element.Element(xmlns + "views");
            var views = Enumerable.Empty<View>();
            if (xviews != null)
            {
                views = xviews.Elements().Select(xview => View.Parse(xview));
            }

            return new TableIndex(version, dbName, databaseProduct, tables, views);
        }

        public static TableIndex ParseFile(string path, ILogger logger, Action<Exception> callback = null)
        {
            var tableIndexDocument = XDocument.Load(path);

            return Parse(tableIndexDocument.Root, logger, callback);
        }
    }
}
