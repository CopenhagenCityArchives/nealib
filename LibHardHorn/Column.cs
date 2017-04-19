﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HardHorn.ArchiveVersion
{
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
        // TIME_TIMEZONE,
        TIMESTAMP,
        // TIMESTAMP_TIMEZONE,
        INTERVAL
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

        public static bool TryParse(Table table, XNamespace ns, XElement xcolumn, out Column column)
        {
            string xml = xcolumn.ToString();
            column = null;
            XElement xname, xtype, xnullable, xdesc, xcolid;
            try
            {
                xname = xcolumn.Element(ns + "name");
                xtype = xcolumn.Element(ns + "type");
                xnullable = xcolumn.Element(ns + "nullable");
                xdesc = xcolumn.Element(ns + "description");
                xcolid = xcolumn.Element(ns + "columnID");
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            string name;
            if (xname.Value.Length > 0)
                name = xname.Value;
            else
                return false;

            bool nullable;
            // parse nullable
            if (xnullable.Value.ToLower() == "true")
                nullable = true;
            else if (xnullable.Value.ToLower() == "false")
                nullable = false;
            else
            {
                return false;
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
            else if ((stype.StartsWith("CHARACTER") && ParseParam(1, stype.Substring(8), out param)) ||
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
            else if (stype.StartsWith("DOUBLE PRECISION") && ParseParam(1, stype.Substring(16), out param))
                column = new Column(table, name, DataType.DOUBLE_PRECISION, nullable, param, desc, colId);
            else if (stype.StartsWith("REAL") && ParseParam(1, 2, stype.Substring(4), out param))
                column = new Column(table, name, DataType.REAL, nullable, param, desc, colId);
            // Boolean types
            else if (stype.StartsWith("BOOLEAN"))
                column = new Column(table, name, DataType.BOOLEAN, nullable, null, desc, colId);
            // Date / time types
            else if (stype.StartsWith("DATE"))
                column = new Column(table, name, DataType.DATE, nullable, null, desc, colId);
            else if (stype.StartsWith("TIMESTAMP"))
                column = new Column(table, name, DataType.TIMESTAMP, nullable, null, desc, colId);
            else if (stype.StartsWith("TIME"))
                column = new Column(table, name, DataType.TIME, nullable, null, desc, colId);
            //TIME_TIMEZONE
            //TIMESTAMP_TIMEZONE
            else if (stype.StartsWith("INTERVAL") && ParseParam(1, stype.Substring(16), out param))
                column = new Column(table, name, DataType.CHARACTER_VARYING, nullable, param, desc, colId);

            return column != null;
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
