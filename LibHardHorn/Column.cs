using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HardHorn.Archiving
{
    public class ArchiveVersionColumnParsingException : Exception
    {
        public XElement Element { get; set; }

        public ArchiveVersionColumnParsingException(string message, XElement element) : base(message)
        {
            Element = element;
        }
    }

    public class ArchiveVersionColumnTypeParsingException : ArchiveVersionColumnParsingException
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Id { get; set; }

        public ArchiveVersionColumnTypeParsingException(string message, string id, string name, string type, XElement element) : base(message, element)
        {
            Name = name;
            Type = type;
            Id = id;
        }
    }

    public enum DataType
    {
        // Text / string / hexadecimal types
        CHARACTER,
        NATIONAL_CHARACTER,
        CHARACTER_VARYING,
        NATIONAL_CHARACTER_VARYING,
        // Integer types
        INTEGER,
        SMALL_INTEGER,
        // Decimal types
        NUMERIC,
        DECIMAL,
        FLOAT,
        DOUBLE_PRECISION,
        REAL,
        // Boolean types
        BOOLEAN,
        // Date / time types
        DATE,
        TIME,
        TIME_WITH_TIME_ZONE,
        TIMESTAMP,
        TIMESTAMP_WITH_TIME_ZONE,
        INTERVAL,
        UNDEFINED,
    }

    public class ParameterizedDataType : IComparable
    {
        public DataType DataType { get; private set; }
        public DataTypeParam Parameter { get; private set; }

        public ParameterizedDataType(DataType dataType, DataTypeParam parameter)
        {
            DataType = dataType;
            Parameter = parameter;
        }

        public int CompareTo(object obj)
        {
            var other = obj as ParameterizedDataType;
            if (other != null)
            {
                var dataTypeComp = DataType.CompareTo(other.DataType);
                if (dataTypeComp != 0)
                {
                    return dataTypeComp;
                }
                else if (Parameter != null)
                {
                    return Parameter.CompareTo(other.Parameter);
                }
                else
                {
                    return other.Parameter == null ? 0 : -1;
                }
            }
            else
            {
                return 1;
            }
        }
    }

    /// <summary>
    /// A parameter list.
    /// </summary>
    public class DataTypeParam : IComparable
    {
        int[] _param;

        public int Length { get { if (_param == null) { return 0; } else { return _param.Length; } } }

        public int this[int index]
        {
            get { return _param[index]; }
            set { _param[index] = value; }
        }

        /// <summary>
        /// Get the string representation.
        /// </summary>
        /// <returns>The string representation of the parameter list.</returns>
        public override string ToString()
        {
            if (_param == null)
            {
                return "";
            }
            else
            {
                return "(" + string.Join(", ", _param.Select(p => p.ToString())) + ")";
            }
        }

        public int CompareTo(object obj)
        {
            var other = obj as DataTypeParam;
            if (other != null)
            {
                if (Length == other.Length)
                {
                    for (int i = 0; i < Length; ++i)
                    {
                        var comp = this[i].CompareTo(other[i]);
                        if (comp != 0)
                        {
                            return comp;
                        }
                    }
                    return 0;
                }
                else if (Length > other.Length)
                {
                    for (int i = 0; i < other.Length; ++i)
                    {
                        var comp = this[i].CompareTo(other[i]);
                        if (comp != 0)
                        {
                            return comp;
                        }
                    }
                    return 1;
                }
                else // Length < other.Length
                {
                    for (int i = 0; i < Length; ++i)
                    {
                        var comp = this[i].CompareTo(other[i]);
                        if (comp != 0)
                        {
                            return comp;
                        }
                    }
                    return -1;
                }
            }
            else
            {
                for (int i = 0; i < Length; ++i)
                {
                    var comp = this[i].CompareTo(obj);
                    if (comp != 0)
                    {
                        return comp;
                    }
                }
                return 0;
            }
        }

        /// <summary>
        /// Construct a parameter list.
        /// </summary>
        /// <param name="param">The integer parameters.</param>
        public DataTypeParam(params int[] param)
        {
            _param = param;
        }
    }

    public class ColumnComparison
    {
        public Column NewColumn { get; set; }
        public Column OldColumn { get; set; }
        public bool Added { get; set; }
        public bool Modified { get; set; }
        public bool Removed { get; set; }
        public bool DataTypeModified { get; set; }
        public bool NullableModified { get; set; }
        public bool DescriptionModified { get; set; }
        public bool IdModified { get; set; }
        public string Name { get; internal set; }

        public ColumnComparison(Column newColumn, Column oldColumn)
        {
            NewColumn = newColumn;
            OldColumn = oldColumn;
            Added = false;
            Modified = false;
            Removed = false;
            DataTypeModified = false;
            NullableModified = false;
            DescriptionModified = false;
            IdModified = false;
        }
    }

    /// <summary>
    /// A column of a table in an archive version.
    /// </summary>
    public class Column
    {
        string _name;
        public string Name { get { return _name; } }

        ParameterizedDataType _dataType;
        public ParameterizedDataType ParameterizedDataType { get { return _dataType; } }

        string _desc;
        public string Description { get { return _desc; } }

        string _colId;
        public string ColumnId { get { return _colId; } }

        int _colIdNum;
        public int ColumnIdNumber { get { return _colIdNum; } }

        bool _nullable;
        public bool Nullable { get { return _nullable; } }

        Table _table;
        public Table Table { get { return _table; } }

        static Regex paramRegex = new Regex(@"^ *\((\d+(,\d+)*)\)$");
        static Regex optionalParamRegex = new Regex(@"^ *\((\d+(,\d+)*)?\)$");
        static Regex commaNumRegex = new Regex(@",?(\d+)");

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
        public Column(Table table, string name, DataType type, bool nullable, int[] param, string desc, string colId, int colIdNum)
        {
            _table = table;
            _name = name;
            _dataType = new ParameterizedDataType(type, param != null ? new DataTypeParam(param) : null);
            _nullable = nullable;
            _desc = desc;
            _colId = colId;
            _colIdNum = colIdNum;
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
            Column column = null;
            XElement xname, xtype, xnullable, xdesc, xcolid;
            try
            {
                xname = xcolumn.Element(xmlns + "name");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column name.", xcolumn);
            }
            try
            {
                xtype = xcolumn.Element(xmlns + "type");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column datatype.", xcolumn);
            }
            try
            {
                xnullable = xcolumn.Element(xmlns + "nullable");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column nullable value.", xcolumn);
            }
            try
            {
                xdesc = xcolumn.Element(xmlns + "description");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column description.", xcolumn);
            }
            try
            {
                xcolid = xcolumn.Element(xmlns + "columnID");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column ID.", xcolumn);
            }

            string name;
            if (xname.Value.Length > 0)
                name = xname.Value;
            else
                throw new ArchiveVersionColumnParsingException("Column name has length 0.", xcolumn);

            bool nullable;
            // parse nullable
            if (xnullable.Value.ToLower() == "true")
                nullable = true;
            else if (xnullable.Value.ToLower() == "false")
                nullable = false;
            else
            {
                throw new ArchiveVersionColumnParsingException("Column has invalid nullable value.", xnullable);
            }

            string desc = xdesc.Value;
            string colId = xcolid.Value;
            int colIdNum = int.Parse(colId.Substring(1));

            // parse type
            string stype = xtype.Value.ToUpper();

            int[] param = null;
            bool usedDefault;
            DataType? dataType = null;
            // Text / string / hexadecimal types
            if ((stype.StartsWith("CHARACTER VARYING") && ParseParam(1, stype.Substring(17), out param, out usedDefault)) ||
                (stype.StartsWith("VARCHAR") && ParseParam(1, stype.Substring(7), out param, out usedDefault)))
                dataType = DataType.CHARACTER_VARYING;
            else if ((stype.StartsWith("CHARACTER") && ParseParam(1, stype.Substring(9), out param, out usedDefault)) ||
                     (stype.StartsWith("CHAR") && ParseParam(1, stype.Substring(4), out param, out usedDefault)))
                dataType = DataType.CHARACTER;
            else if ((stype.StartsWith("NATIONAL CHARACTER VARYING") && ParseParam(1, stype.Substring(26), out param, out usedDefault)) ||
                     (stype.StartsWith("NATIONAL VARCHAR") && ParseParam(1, stype.Substring(16), out param, out usedDefault)) ||
                     (stype.StartsWith("NVARCHAR") && ParseParam(1, stype.Substring(8), out param, out usedDefault)))
                dataType = DataType.NATIONAL_CHARACTER_VARYING;
            else if ((stype.StartsWith("NATIONAL CHARACTER") && ParseParam(1, stype.Substring(18), out param, out usedDefault)) ||
                     (stype.StartsWith("NATIONAL CHAR") && ParseParam(1, stype.Substring(13), out param, out usedDefault)) ||
                     (stype.StartsWith("NCHAR") && ParseParam(1, stype.Substring(5), out param, out usedDefault)))
                dataType = DataType.NATIONAL_CHARACTER;
            // Integer types
            else if (stype.StartsWith("INTEGER"))
                dataType = DataType.INTEGER;
            else if (stype.StartsWith("SMALL INTEGER"))
                dataType = DataType.SMALL_INTEGER;
            // Decimal types
            else if (stype.StartsWith("NUMERIC") && ParseParam(1, 2, stype.Substring(7), out param, out usedDefault))
                dataType = DataType.NUMERIC;
            else if (stype.StartsWith("DECIMAL") && ParseParam(1, 2, stype.Substring(7), out param, out usedDefault))
                dataType = DataType.DECIMAL;
            else if (stype.StartsWith("FLOAT") && ParseParam(1, stype.Substring(5), out param, out usedDefault))
                dataType = DataType.FLOAT;
            else if (stype.StartsWith("DOUBLE PRECISION"))
                dataType = DataType.DOUBLE_PRECISION;
            else if (stype.StartsWith("REAL"))
                dataType = DataType.REAL;
            // Boolean types
            else if (stype.StartsWith("BOOLEAN"))
                dataType = DataType.BOOLEAN;
            // Date / time types
            else if (stype.StartsWith("DATE"))
                dataType = DataType.DATE;
            else if (stype.StartsWith("TIMESTAMP WITH TIME ZONE"))
                dataType = DataType.TIMESTAMP_WITH_TIME_ZONE;
            else if (stype.StartsWith("TIMESTAMP WITHOUT TIME ZONE"))
                dataType = DataType.TIMESTAMP;
            else if (stype.StartsWith("TIMESTAMP"))
                dataType = DataType.TIMESTAMP;
            else if (stype.StartsWith("TIME WITHOUT TIME ZONE"))
                dataType = DataType.TIME;
            else if (stype.StartsWith("TIME WITH TIME ZONE"))
                dataType = DataType.TIME_WITH_TIME_ZONE;
            else if (stype.StartsWith("TIME"))
                dataType = DataType.TIME;
            else if (stype.StartsWith("INTERVAL") && ParseParam(1, stype.Substring(16), out param, out usedDefault))
                dataType = DataType.CHARACTER_VARYING;

            if (dataType.HasValue)
                column = new Column(table, name, dataType.Value, nullable, param, desc, colId, colIdNum);

            if (column == null)
                throw new ArchiveVersionColumnTypeParsingException("Could not parse column data type and parameters for type: \"" + stype + "\"", colId, name, stype, xtype);
            else
                return column;
        }

        /// <summary>
        /// A helper method to parse a parameter string.
        /// </summary>
        /// <param name="length">The number of parameters.</param>
        /// <param name="s">The parameter string.</param>
        /// <param name="param">A reference to the integer array that should contain the parameter list.</param>
        /// <returns></returns>
        static bool ParseParam(int length, string s, out int[] param, out bool usedDefault, int[] defaultParam = null)
        {
            return ParseParam(length, length, s, out param, out usedDefault, defaultParam);
        }

        /// <summary>
        /// A helper method to parse a parameter string.
        /// </summary>
        /// <param name="minLength">The minimum number of parameters.</param>
        /// <param name="maxLength">The maximum number of parameters.</param>
        /// <param name="s">The parameter string.</param>
        /// <param name="param">A reference to the integer array that should contain the parameter list.</param>
        /// <returns></returns>
        static bool ParseParam(int minLength, int maxLength, string s, out int[] param, out bool usedDefault, int[] defaultParam = null)
        {
            Match parenMatch;

            if (defaultParam != null)
            {
                parenMatch = optionalParamRegex.Match(s);
            }
            else
            {
                parenMatch = paramRegex.Match(s);
            }

            if (parenMatch.Success)
            {
                if (parenMatch.Groups.Count == 0 && defaultParam != null)
                {
                    param = new int[defaultParam.Length];
                    for (int i = 0; i < defaultParam.Length; i++)
                        param[i] = defaultParam[i];
                    usedDefault = true;
                    return true;
                }

                usedDefault = false;

                var numMatches = commaNumRegex.Matches(parenMatch.Groups[0].Value);

                if (numMatches.Count >= minLength && numMatches.Count <= maxLength)
                {
                    param = new int[numMatches.Count];

                    for (int i = 0; i < numMatches.Count; i++)
                    {
                        param[i] = int.Parse(numMatches[i].Groups[1].Value);
                    }

                    usedDefault = false;
                    return true;
                }
            }

            param = null;
            usedDefault = false;
            return false;
        }
    }
}