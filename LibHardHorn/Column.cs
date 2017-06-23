using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HardHorn.ArchiveVersion
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

        public ArchiveVersionColumnTypeParsingException(string message, string name, string type, XElement element) : base(message, element)
        {
            Name = name;
            Type = type;
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
        NOT_DEFINED,
    }

    public class DataTypeParam
    {
        int[] _param;

        public int Length { get { if (_param == null) { return 0; } else { return _param.Length; } } }

        public int this[int index]
        {
            get { return _param[index]; }
            set { _param[index] = value; }
        }

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

        public DataTypeParam(params int[] param)
        {
            _param = param;
        }


    }

    public class Column
    {
        string _name;
        public string Name { get { return _name; } }

        DataType _type;
        public DataType Type { get { return _type; } }

        string _desc;
        public string Description { get { return _desc; } }

        string _colId;
        public string ColumnId { get { return _colId; } }

        bool _nullable;
        public bool Nullable { get { return _nullable; } }

        DataTypeParam _param;
        public DataTypeParam Param { get { return _param; } }

        Table _table;
        public Table Table { get { return _table; } }

        static Regex paramRegex = new Regex(@"^ *\((\d+(,\d+)*)\)$");
        static Regex commaNumRegex = new Regex(@",?(\d+)");

        public Column(Table table, string name, DataType type, bool nullable, int[] param, string desc, string colId)
        {
            _table = table;
            _name = name;
            _type = type;
            _param = param != null ? new DataTypeParam(param) : null;
            _nullable = nullable;
            _desc = desc;
            _colId = colId;
        }

        public static Column Parse(Table table, XNamespace ns, XElement xcolumn)
        {
            string xml = xcolumn.ToString();
            Column column = null;
            XElement xname, xtype, xnullable, xdesc, xcolid;
            try
            {
                xname = xcolumn.Element(ns + "name");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column name.", xcolumn);
            }
            try
            {
                xtype = xcolumn.Element(ns + "type");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column datatype.", xcolumn);
            }
            try
            {
                xnullable = xcolumn.Element(ns + "nullable");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column nullable value.", xcolumn);
            }
            try
            {
                xdesc = xcolumn.Element(ns + "description");
            }
            catch (InvalidOperationException)
            {
                throw new ArchiveVersionColumnParsingException("Could not read column description.", xcolumn);
            }
            try
            {
                xcolid = xcolumn.Element(ns + "columnID");
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

            // parse type
            string stype = xtype.Value.ToUpper();

            int[] param;
            // Text / string / hexadecimal types
            if ((stype.StartsWith("CHARACTER VARYING") && ParseParam(1, stype.Substring(17), out param)) ||
                (stype.StartsWith("VARCHAR") && ParseParam(1, stype.Substring(7), out param)))
                column = new Column(table, name, DataType.CHARACTER_VARYING, nullable, param, desc, colId);
            else if ((stype.StartsWith("CHARACTER") && ParseParam(1, stype.Substring(9), out param)) ||
                     (stype.StartsWith("CHAR") && ParseParam(1, stype.Substring(4), out param)))
                column = new Column(table, name, DataType.CHARACTER, nullable, param, desc, colId);
            else if ((stype.StartsWith("NATIONAL CHARACTER VARYING") && ParseParam(1, stype.Substring(26), out param)) ||
                     (stype.StartsWith("NATIONAL VARCHAR") && ParseParam(1, stype.Substring(16), out param)) ||
                     (stype.StartsWith("NVARCHAR") && ParseParam(1, stype.Substring(8), out param)))
                column = new Column(table, name, DataType.NATIONAL_CHARACTER_VARYING, nullable, param, desc, colId);
            else if ((stype.StartsWith("NATIONAL CHARACTER") && ParseParam(1, stype.Substring(18), out param)) ||
                     (stype.StartsWith("NATIONAL CHAR") && ParseParam(1, stype.Substring(13), out param)) ||
                     (stype.StartsWith("NCHAR") && ParseParam(1, stype.Substring(5), out param)))
                column = new Column(table, name, DataType.NATIONAL_CHARACTER, nullable, param, desc, colId);
            // Integer types
            else if (stype.StartsWith("INTEGER"))
                column = new Column(table, name, DataType.INTEGER, nullable, null, desc, colId);
            else if (stype.StartsWith("SMALL INTEGER"))
                column = new Column(table, name, DataType.SMALL_INTEGER, nullable, null, desc, colId);
            // Decimal types
            else if (stype.StartsWith("NUMERIC") && ParseParam(1, 2, stype.Substring(7), out param))
                column = new Column(table, name, DataType.NUMERIC, nullable, param, desc, colId);
            else if (stype.StartsWith("DECIMAL") && ParseParam(1, 2, stype.Substring(7), out param))
                column = new Column(table, name, DataType.DECIMAL, nullable, param, desc, colId);
            else if (stype.StartsWith("FLOAT") && ParseParam(1, stype.Substring(5), out param))
                column = new Column(table, name, DataType.FLOAT, nullable, param, desc, colId);
            else if (stype.StartsWith("DOUBLE PRECISION"))
                column = new Column(table, name, DataType.DOUBLE_PRECISION, nullable, null, desc, colId);
            else if (stype.StartsWith("REAL"))
                column = new Column(table, name, DataType.REAL, nullable, null, desc, colId);
            // Boolean types
            else if (stype.StartsWith("BOOLEAN"))
                column = new Column(table, name, DataType.BOOLEAN, nullable, null, desc, colId);
            // Date / time types
            else if (stype.StartsWith("DATE"))
                column = new Column(table, name, DataType.DATE, nullable, null, desc, colId);
            else if (stype.StartsWith("TIMESTAMP WITH TIME ZONE"))
                column = new Column(table, name, DataType.TIMESTAMP_WITH_TIME_ZONE, nullable, null, desc, colId);
            else if (stype.StartsWith("TIMESTAMP WITHOUT TIME ZONE"))
                column = new Column(table, name, DataType.TIMESTAMP, nullable, null, desc, colId);
            else if (stype.StartsWith("TIMESTAMP"))
                column = new Column(table, name, DataType.TIMESTAMP, nullable, null, desc, colId);
            else if (stype.StartsWith("TIME WITHOUT TIME ZONE"))
                column = new Column(table, name, DataType.TIME, nullable, null, desc, colId);
            else if (stype.StartsWith("TIME WITH TIME ZONE"))
                column = new Column(table, name, DataType.TIME_WITH_TIME_ZONE, nullable, null, desc, colId);
            else if (stype.StartsWith("TIME"))
                column = new Column(table, name, DataType.TIME, nullable, null, desc, colId);
            else if (stype.StartsWith("INTERVAL") && ParseParam(1, stype.Substring(16), out param))
                column = new Column(table, name, DataType.CHARACTER_VARYING, nullable, param, desc, colId);

            if (column == null)
                throw new ArchiveVersionColumnTypeParsingException("Could not parse column data type and parameters for type: \"" + stype + "\"", name, stype, xtype);
            else
                return column;
        }

        static bool ParseParam(int n, string s, out int[] param)
        {
            return ParseParam(n, n, s, out param);
        }

        static bool ParseParam(int n, int m, string s, out int[] param)
        {
            var parenMatch = paramRegex.Match(s);

            if (parenMatch.Success)
            {
                var numMatches = commaNumRegex.Matches(parenMatch.Groups[0].Value);

                if (numMatches.Count >= n && numMatches.Count <= m)
                {
                    param = new int[numMatches.Count];

                    for (int i = 0; i < numMatches.Count; i++)
                    {
                        param[i] = int.Parse(numMatches[i].Groups[1].Value);
                    }

                    return true;
                }
            }

            param = null;
            return false;
        }
    }
}
