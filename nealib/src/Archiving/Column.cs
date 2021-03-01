﻿using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using NEA.Utility;

namespace NEA.Archiving
{
    /// <summary>
    /// A column of a table in an archive version.
    /// </summary>
    public class Column
    {
        public Table Table { get; private set; }
        public string Name { get; private set; }
        public ParameterizedDataType ParameterizedDataType { get; set; }
        public string DataTypeOriginal { get; private set; }
        public string Description { get; set; }
        public string ColumnId { get; private set; }
        public int ColumnIdNumber { get; private set; }
        public bool Nullable { get; set; }
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

        public override string ToString()
        {
            return string.Format("<{0}: {1} {2}>", ColumnId, Name, ParameterizedDataType.ToString());
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
            else if (ParameterizedDataType.Parameter.CompareTo(oldColumn.ParameterizedDataType.Parameter) != 0)
            {
                comparison.Modified = true;
                comparison.DataTypeModified = true;
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

        public XElement ToXml(bool overwriteUnchangedDataTypes = false)
        {
            XNamespace xmlns = "http://www.sa.dk/xmlns/diark/1.0";

            return new XElement(xmlns + "column",
                new XElement(xmlns + "name", Name),
                new XElement(xmlns + "columnID", ColumnId),
                new XElement(xmlns + "type", ParameterizedDataType.ToString(overwriteUnchangedDataTypes)),
                DataTypeOriginal == null ? null : new XElement(xmlns + "typeOriginal", DataTypeOriginal),
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
        public static Column Parse(Table table, XElement xcolumn, NotificationCallback notify)
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
                notify?.Invoke(new ColumnParsingErrorNotification(table, "Kolonnenavn mangler"));
                throw new ColumnParsingException("Could not read column name.", xcolumn, table);
            }
            try
            {
                xtype = xcolumn.Element(xmlns + "type");
            }
            catch (InvalidOperationException)
            {
                notify?.Invoke(new ColumnParsingErrorNotification(table, "Kolonnetype mangler"));
                throw new ColumnParsingException("Could not read column datatype.", xcolumn, table);
            }
            try
            {
                xtypeorig = xcolumn.Element(xmlns + "typeOriginal");
            }
            catch (InvalidOperationException)
            {
                notify?.Invoke(new ColumnParsingErrorNotification(table, "Oprindelig kolonnetype mangler"));
                throw new ColumnParsingException("Could not read column original datatype", xcolumn, table);
            }
            try
            {
                xnullable = xcolumn.Element(xmlns + "nullable");
            }
            catch (InvalidOperationException)
            {
                notify?.Invoke(new ColumnParsingErrorNotification(table, "Kolonnens nullbarhed mangler"));
                throw new ColumnParsingException("Could not read column nullable value.", xcolumn, table);
            }
            try
            {
                xdesc = xcolumn.Element(xmlns + "description");
            }
            catch (InvalidOperationException)
            {
                notify?.Invoke(new ColumnParsingErrorNotification(table, "Kolonnens beskrivelse mangler"));
                throw new ColumnParsingException("Could not read column description.", xcolumn, table);
            }
            try
            {
                xcolid = xcolumn.Element(xmlns + "columnID");
            }
            catch (InvalidOperationException)
            {
                notify?.Invoke(new ColumnParsingErrorNotification(table, "Kolonnens Id mangler"));
                throw new ColumnParsingException("Could not read column ID.", xcolumn, table);
            }
            string defaultValue = null;
            var defaultValueElement = xcolumn.Element(xmlns + "defaultValue");
            if (defaultValueElement != null)
            {
                defaultValue = defaultValueElement.Value;
            }

            string functionalDescription = null;
            var functionalDescriptionElement = xcolumn.Element(xmlns + "functionalDescription");
            if (functionalDescriptionElement != null)
            {
                functionalDescription = functionalDescriptionElement.Value;
            }

            string name;
            if (xname.Value.Length > 0)
                name = xname.Value;
            else
            {
                notify?.Invoke(new ColumnParsingErrorNotification(table, "Kolonnens er tomt"));
                throw new ColumnParsingException("Column name has length 0.", xcolumn, table);
            }

            bool nullable;
            // parse nullable
            if (xnullable.Value.ToLower() == "true")
                nullable = true;
            else if (xnullable.Value.ToLower() == "false")
                nullable = false;
            else
            {
                notify?.Invoke(new ColumnParsingErrorNotification(table, $"'{xnullable.Value}' er ikke en gyldig værdig for nullable"));
                throw new ColumnParsingException("Column has invalid nullable value.", xnullable, table);
            }

            string desc = xdesc.Value;
            string colId = xcolid.Value;
            int colIdNum = int.Parse(colId.Substring(1));

            string typeOrig = xtypeorig == null ? null : xtypeorig.Value;
            var column = new Column(table, name, new ParameterizedDataType(DataType.UNDEFINED, null), typeOrig, nullable, desc, colId, colIdNum, defaultValue, functionalDescription);

            // parse type
            ParameterizedDataType parameterizedDataType = ParameterizedDataType.Parse(xtype, table, column, notify);
            column.ParameterizedDataType = parameterizedDataType;

            return column;
        }
    }
}