using HardHorn.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using HardHorn.Utility;

namespace HardHorn.Archiving
{
    /// <summary>
    /// A table in an archive version.
    /// </summary>
    public class Table
    {
        public ArchiveVersion ArchiveVersion { get; set; }

        public List<Column> Columns { get; private set; }

        public string Name { get; private set; }

        public string Folder { get; private set; }

        public int Rows { get; private set; }

        public string Description { get; private set; }

        public PrimaryKey PrimaryKey { get; private set; }
        public List<ForeignKey> ForeignKeys {get;private set;}

        /// <summary>
        /// Construct a table.
        /// </summary>
        /// <param name="name">The name of the table.</param>
        /// <param name="folder">The folder of the table.</param>
        /// <param name="rows">The number of rows in the table.</param>
        /// <param name="description">A description of the table.</param>
        /// <param name="columns">The columns of the table.</param>
        public Table(string name, string folder, int rows, string description, List<Column> columns, PrimaryKey primaryKey, IEnumerable<ForeignKey> foreignKeys)
        {
            Name = name;
            Folder = folder;
            Columns = columns;
            Rows = rows;
            Description = description;
            PrimaryKey = primaryKey;
            ForeignKeys = new List<ForeignKey>(foreignKeys);
        }

        /// <summary>
        /// Parse an XML element, to get a table.
        /// </summary>
        /// <param name="archiveVersion">The archive version, the table is a part of.</param>
        /// <param name="ns">The XML namespace to use.</param>
        /// <param name="xtable">The XML element to parse.</param>
        /// <param name="log">The logger.</param>
        /// <returns></returns>
        public static Table Parse(XElement xtable, ILogger log, Action<Exception> callback = null)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            string name = xtable.Element(xmlns + "name").Value;
            string folder = xtable.Element(xmlns + "folder").Value;
            int rows = int.Parse(xtable.Element(xmlns + "rows").Value);
            string desc = xtable.Element(xmlns + "description").Value;
            var pkey = PrimaryKey.Parse(xtable.Element(xmlns + "primaryKey"));
            var xfkeys = xtable.Element(xmlns + "foreignKeys");
            var fkeys = Enumerable.Empty<ForeignKey>();
            if (xfkeys != null)
            {
                fkeys = xfkeys.Elements().Select(xfkey => ForeignKey.Parse(xfkey));
            }

            var table = new Table(name, folder, rows, desc, new List<Column>(), pkey, fkeys);

            int dummyCount = 1;
            var xcolumns = xtable.Element(xmlns + "columns");
            foreach (var xcolumn in xcolumns.Elements(xmlns + "column"))
            {
                try
                {
                    var column = Column.Parse(table, xcolumn);
                    (table.Columns as List<Column>).Add(column);
                }
                catch (ArchiveVersionColumnTypeParsingException ex)
                {
                    log.Log(string.Format("En fejl opstod under afkodningen af kolonnen '{0}' i tabellen '{1}': Typen '{2}' er ikke valid.", ex.Name, table.Name, ex.Type), LogLevel.ERROR);
                    (table.Columns as List<Column>).Add(new Column(table, ex.Name, ParameterizedDataType.GetUndefined(), null, false, "", ex.Id, int.Parse(ex.Id.Substring(1)), null, null));
                    if (callback != null)
                        callback(ex);
                }
                catch (ArchiveVersionColumnParsingException ex)
                {
                    log.Log(string.Format("En fejl opstod under afkodningen af en kolonne i tabellen '{0}': {1}", table.Name, ex.Message), LogLevel.ERROR);
                    (table.Columns as List<Column>).Add(new Column(table, "__Ugyldig_Kolonne" + (dummyCount++).ToString() + "__", ParameterizedDataType.GetUndefined(), null, false, "", "", 0, null, null));
                    if (callback != null)
                        callback(ex);
                }
            }

            return table;
        }

        /// <summary>
        /// Get a reader.
        /// </summary>
        /// <returns>The reader.</returns>
        public TableReader GetReader()
        {
            return new TableReader(this);
        }

        public TableComparison CompareTo(Table oldTable)
        {
            var tableComparison = new TableComparison(this, oldTable);
            tableComparison.Name = Name;

            if (Rows != oldTable.Rows)
            {
                tableComparison.Modified = true;
                tableComparison.RowsModified = true;
            }

            if (Description != oldTable.Description)
            {
                tableComparison.Modified = true;
                tableComparison.DescriptionModified = true;
            }

            if (Folder != oldTable.Folder)
            {
                tableComparison.Modified = true;
                tableComparison.FolderModified = true;
            }

            foreach (var column in Columns)
            {
                bool columnAdded = true;
                foreach (var oldColumn in oldTable.Columns)
                {
                    if (column.Name.ToLower() == oldColumn.Name.ToLower())
                    {
                        var columnComparison = column.CompareTo(oldColumn);
                        columnComparison.Name = column.Name;
                        tableComparison.Columns.Add(columnComparison);
                        columnAdded = false;
                        break;
                    }
                }

                if (columnAdded)
                {
                    tableComparison.Columns.Add(new ColumnComparison(column, null) { Name = column.Name, Added = true });
                }
            }

            foreach (var oldColumn in oldTable.Columns)
            {
                bool columnRemoved = true;
                foreach (var column in Columns)
                {
                    if (column.Name.ToLower() == oldColumn.Name.ToLower())
                    {
                        columnRemoved = false;
                    }
                }

                if (columnRemoved)
                {
                    tableComparison.Columns.Add(new ColumnComparison(null, oldColumn) { Name = oldColumn.Name, Removed = true });
                }
            }

            foreach (dynamic col in tableComparison.Columns)
            {
                tableComparison.Modified = col.Modified || tableComparison.Modified;
                tableComparison.ColumnsModified = col.Modified || tableComparison.ColumnsModified;
            }

            return tableComparison;
        }

        internal XElement ToXml()
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            return new XElement(xmlns + "table",
                new XElement(xmlns + "name", Name),
                new XElement(xmlns + "folder", Folder),
                new XElement(xmlns + "description", Description),
                new XElement(xmlns + "columns", Columns.Select(c => c.ToXml())),
                PrimaryKey.ToXml(),
                ForeignKeys.Count > 0 ? new XElement(xmlns + "foreignKeys", ForeignKeys.Select(fKey => fKey.ToXml())) : null,
                new XElement(xmlns + "rows", Rows));
        }
    }
}
