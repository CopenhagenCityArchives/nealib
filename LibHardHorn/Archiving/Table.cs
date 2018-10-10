using HardHorn.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using HardHorn.Utility;
using System.IO;

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

        public string Description { get; set; }

        public PrimaryKey PrimaryKey { get; private set; }
        public List<ForeignKey> ForeignKeys {get;private set;}

        public override string ToString()
        {
            return string.Format("<{0}: {1}>", Folder, Name);
        }

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
        public static Table Parse(XElement xtable, ILogger log, NotificationCallback notify)
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
                    var column = Column.Parse(table, xcolumn, notify);
                    (table.Columns as List<Column>).Add(column);
                }
                catch (ColumnTypeParsingException ex)
                {
                    notify?.Invoke(new ColumnTypeErrorNotification(ex.Column, ex.Message));
                    (table.Columns as List<Column>).Add(ex.Column);
                    //if (callback != null)
                    //    callback(ex);
                }
                catch (ColumnParsingException ex)
                {
                    var column = new Column(table, "__Ugyldig_Kolonne" + (dummyCount++).ToString() + "__", ParameterizedDataType.GetUndefined(), null, false, "", "", 0, null, null);
                    (table.Columns as List<Column>).Add(column);
                    //if (callback != null)
                    //    callback(ex);
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


            // Compare columns
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

            foreach (var col in tableComparison.Columns)
            {
                tableComparison.Modified = col.Modified || tableComparison.Modified;
                tableComparison.ColumnsModified = col.Modified || tableComparison.ColumnsModified;
            }

            // Compare foreign keys
            foreach (var foreignKey in ForeignKeys)
            {
                bool foreignKeyAdded = true;
                foreach (var oldForeignKey in oldTable.ForeignKeys)
                {
                    if (foreignKey.Name.ToLower() == oldForeignKey.Name.ToLower())
                    {
                        var foreignKeyComparison = foreignKey.CompareTo(oldForeignKey);
                        foreignKeyComparison.Name = foreignKey.Name;
                        tableComparison.ForeignKeys.Add(foreignKeyComparison);
                        foreignKeyAdded = false;
                        break;
                    }
                }

                if (foreignKeyAdded)
                {
                    tableComparison.ForeignKeys.Add(new ForeignKeyComparison(foreignKey, null) { Name = foreignKey.Name, Added = true });
                }
            }

            foreach (var oldForeignKey in oldTable.ForeignKeys)
            {
                bool foreignKeyRemoved = true;
                foreach (var foreignKey in ForeignKeys)
                {
                    if (foreignKey.Name.ToLower() == oldForeignKey.Name.ToLower())
                    {
                        foreignKeyRemoved = false;
                    }
                }

                if (foreignKeyRemoved)
                {
                    tableComparison.ForeignKeys.Add(new ForeignKeyComparison(null, oldForeignKey) { Name = oldForeignKey.Name, Removed = true });
                }
            }

            foreach (var foreignKey in tableComparison.ForeignKeys)
            {
                tableComparison.Modified = foreignKey.Modified || tableComparison.Modified;
                tableComparison.ForeignKeysModified = foreignKey.Modified || tableComparison.ForeignKeysModified;
            }

            return tableComparison;
        }

        public void WriteTableFileFromReplacements(List<ReplacementOperation> replacements, StreamWriter writer)
        {

            using (var reader = GetReader())
            {
                Post[,] rows;
                while (reader.Read(out rows) > 0)
                {

                }
            }
        }

        internal XElement ToXml(bool overwriteUnchangedDataTypes = false)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            return new XElement(xmlns + "table",
                new XElement(xmlns + "name", Name),
                new XElement(xmlns + "folder", Folder),
                new XElement(xmlns + "description", Description),
                new XElement(xmlns + "columns", Columns.Select(c => c.ToXml(overwriteUnchangedDataTypes))),
                PrimaryKey.ToXml(),
                ForeignKeys.Count > 0 ? new XElement(xmlns + "foreignKeys", ForeignKeys.Select(fKey => fKey.ToXml())) : null,
                new XElement(xmlns + "rows", Rows));
        }
    }
}
