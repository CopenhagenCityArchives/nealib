﻿using NEA.Archiving;
using NEA.Logging;
using NEA.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Schema;

namespace NEA.Archiving
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

        public XDocument ToXml(bool overwriteUnchangedDataTypes)
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
                new XElement(xmlns + "tables", Tables.Select(t => t.ToXml(overwriteUnchangedDataTypes))),
                new XElement(xmlns + "views", Views.Select(v => v.ToXml())));

            return new XDocument(root);
        }

        public static TableIndex Parse(XElement element, ILogger logger, NotificationCallback notify)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            var versionElem = element.Element(xmlns + "version");
            if (versionElem == null)
            {
                throw new RequiredFieldMissingException("version");
            }
            var version = versionElem.Value;

            var dbNameElem = element.Element(xmlns + "dbName");
            string dbName = dbNameElem == null ? null : dbNameElem.Value;

            var databaseProductElem = element.Element(xmlns + "databaseProduct");
            string databaseProduct = databaseProductElem == null ? null : databaseProductElem.Value;

            var tables = element.Element(xmlns + "tables").Elements().Select(xtable => Table.Parse(xtable, logger, notify));

            var xviews = element.Element(xmlns + "views");
            var views = Enumerable.Empty<View>();
            if (xviews != null)
            {
                views = xviews.Elements().Select(xview => View.Parse(xview));
            }

            var tableIndex = new TableIndex(version, dbName, databaseProduct, tables, views);

            // Initialize relations
            foreach (var table in tableIndex.Tables)
            {
                foreach (var fkey in table.ForeignKeys)
                {
                    fkey.Initialize(tableIndex, table, notify);
                }
            }

            return tableIndex; 
        }

        public static TableIndex ParseFile(string path, ILogger logger, NotificationCallback notify, bool validate = true)
        {
            var tableIndexDocument = XDocument.Load(path);

            if (validate)
            {
                XNamespace xmlns = "http://www.w3.org/2001/XMLSchema-instance";
                var schemas = new XmlSchemaSet();
                try
                {
                    var schemaLocation = tableIndexDocument.Root.Attribute(xmlns + "schemaLocation").Value.Split(' ').ToArray();
                    if (schemaLocation[1].StartsWith("file:"))
                        schemaLocation[1].Remove(0, 5);
                    var targetNamespace = schemaLocation[0];
                    var schemaUri = System.IO.Path.GetFullPath(System.IO.Path.GetDirectoryName(path) + "\\" + schemaLocation[1].Replace('/', '\\'));
                    schemas.Add(targetNamespace, schemaUri);
                }
                catch (Exception ex)
                {
                    throw new Exception("Kunne ikke indlæse skema fra tableIndex.xml.", ex);
                }

                tableIndexDocument.Validate(schemas, (o, e) =>
                {
                    notify?.Invoke(new XmlErrorNotification(e.Message));
                });
            }

            return Parse(tableIndexDocument.Root, logger, notify);
        }
    }
}
