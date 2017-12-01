using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using HardHorn.Utility;

namespace HardHorn.Archiving
{
    /// <summary>
    /// A column of a table in an archive version.
    /// </summary>
    public class Column
    {
        public string Name { get; private set; }
        public ParameterizedDataType ParameterizedDataType { get; set; }
        public string DataTypeOriginal { get; private set; }
        public string Description { get; private set; }
        public string ColumnId { get; private set; }
        public int ColumnIdNumber { get; private set; }
        public bool Nullable { get; private set; }
        public Table Table { get; private set; }
        public string DefaultValue { get; private set; }
        public string FunctionalDescription { get; private set; }

        /// <summary>
        /// Construct a column.
        /// </summary>
        /// <param name="table">The table, the column is a part of.</param>
        /// <param name="name">The name of the column.</param>
        /// <param name="type">The data type of the column.</param>
        /// <param name="nullable">Is the column nullable.</param>
        /// <param name="param">The parameters of the column datatype.</param>
        /// <param name="desc">The description of the column.</param>
        /// <param name="colId">The id of the column.</param>
        public Column(Table table, string name, ParameterizedDataType type, string dataTypeOrig, bool nullable, string desc, string colId, int colIdNum, string defaultValue, string functionalDescription)
        {
            Table = table;
            Name = name;
            ParameterizedDataType = type;
            Nullable = nullable;
            Description = desc;
            ColumnId = colId;
            ColumnIdNumber = colIdNum;
            DataTypeOriginal = dataTypeOrig;
            DefaultValue = defaultValue;
            FunctionalDescription = functionalDescription;
        }

        public ColumnComparison CompareTo(Column oldColumn)
        {
            var comparison = new ColumnComparison(this, oldColumn);

            if (Description != oldColumn.Description)
            {
                comparison.Modified = true;
                comparison.DescriptionModified = true;
            }

            if (ParameterizedDataType.DataType != oldColumn.ParameterizedDataType.DataType)
            {
                comparison.Modified = true;
                comparison.DataTypeModified = true;
            }

            if (ParameterizedDataType.Parameter == null && oldColumn.ParameterizedDataType.Parameter != null)
            {
                comparison.Modified = true;
                comparison.DataTypeModified = true;
            }
            else if (ParameterizedDataType.Parameter != null && oldColumn.ParameterizedDataType.Parameter == null)
            {
                comparison.Modified = true;
                comparison.DataTypeModified = true;
            }
            else if (ParameterizedDataType.Parameter == null && oldColumn.ParameterizedDataType.Parameter == null)
            { }
            else if (ParameterizedDataType.Parameter.Length != oldColumn.ParameterizedDataType.Parameter.Length)
            {
                comparison.Modified = true;
                comparison.DataTypeModified = true;
            }
            else
            {
                for (int i = 0; i < ParameterizedDataType.Parameter.Length; i++)
                {
                    if (ParameterizedDataType.Parameter[i] != oldColumn.ParameterizedDataType.Parameter[i])
                    {
                        comparison.Modified = true;
                        comparison.DataTypeModified = true;
                        break;
                    }
                }
            }

            if (Nullable != oldColumn.Nullable)
            {
                comparison.Modified = true;
                comparison.NullableModified = true;
            }

            if (ColumnId != oldColumn.ColumnId)
            {
                comparison.Modified = true;
                comparison.IdModified = true;
            }

            return comparison;
        }

        public XElement ToXml()
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";
            var a = "a" ?? "b";

            return new XElement(xmlns + "column",
                new XElement(xmlns + "name", Name),
                new XElement(xmlns + "columnID", ColumnId),
                new XElement(xmlns + "type", ParameterizedDataType.ToString()),
                new XElement(xmlns + "typeOriginal", DataTypeOriginal),
                DefaultValue == null ? null : new XElement(xmlns + "defaultValue", DefaultValue),
                new XElement(xmlns + "nullable", Nullable),
                new XElement(xmlns + "description", Description),
                FunctionalDescription == null ? null : new XElement(xmlns + "functionalDescription", FunctionalDescription));
        }

        /// <summary>
        /// Parse a column object.
        /// </summary>
        /// <param name="table">The table, the column is a part of.</param>
        /// <param name="ns">The XML namespace to use.</param>
        /// <param name="xcolumn">The column XML element.</param>
        /// <returns></returns>
        public static Column Parse(Table table, XElement xcolumn)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            string xml = xcolumn.ToString();
            XElement xname, xtype, xtypeorig, xnullable, xdesc, xcolid;
            try
            {
                xname = xcolumn.Element(xmlns + "name");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column name.", xcolumn, table);
            }
            try
            {
                xtype = xcolumn.Element(xmlns + "type");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column datatype.", xcolumn, table);
            }
            try
            {
                xtypeorig = xcolumn.Element(xmlns + "typeOriginal");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column original datatype", xcolumn, table);
            }
            try
            {
                xnullable = xcolumn.Element(xmlns + "nullable");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column nullable value.", xcolumn, table);
            }
            try
            {
                xdesc = xcolumn.Element(xmlns + "description");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column description.", xcolumn, table);
            }
            try
            {
                xcolid = xcolumn.Element(xmlns + "columnID");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column ID.", xcolumn, table);
            }
            string defaultValue = null;
            try
            {
                defaultValue = xcolumn.Element(xmlns + "defaultValue").Value;
            }
            catch (Exception) { }

            string functionalDescription = null;
            try
            {
                functionalDescription = xcolumn.Element(xmlns + "functionalDescription").Value;
            }
            catch (Exception) { }

            string name;
            if (xname.Value.Length > 0)
                name = xname.Value;
            else
                throw new ArchiveVersionColumnParsingException("Column name has length 0.", xcolumn, table);

            bool nullable;
            // parse nullable
            if (xnullable.Value.ToLower() == "true")
                nullable = true;
            else if (xnullable.Value.ToLower() == "false")
                nullable = false;
            else
            {
                throw new ArchiveVersionColumnParsingException("Column has invalid nullable value.", xnullable, table);
            }

            string desc = xdesc.Value;
            string colId = xcolid.Value;
            int colIdNum = int.Parse(colId.Substring(1));

            // parse type
            ParameterizedDataType parameterizedDataType = ParameterizedDataType.Parse(xtype, table, colId, name);

            string typeOrig = xtypeorig.Value;

            return new Column(table, name, parameterizedDataType, typeOrig, nullable, desc, colId, colIdNum, defaultValue, functionalDescription);
        }
    }
}