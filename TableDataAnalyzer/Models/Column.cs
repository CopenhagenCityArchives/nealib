using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TableDataAnalyzer.Models
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

    class Column
    {
        string _name;
        public string Name { get { return _name; } }

        DataType _type;
        public DataType Type { get { return _type; } }

        bool _nullable;
        public bool Nullable { get { return _nullable; } }

        int[] _param;
        public int[] Param { get { return _param; } }

        static Regex paramRegex = new Regex(@"^\((\d+(,\d+)*)\)$");
        static Regex commaNumRegex = new Regex(@",?(\d+)");

        public Column(string name, DataType type, bool nullable, int[] param)
        {
            _name = name;
            _type = type;
            _param = param;
            _nullable = nullable;
        }

        public static bool TryParse(XNamespace ns, XElement xcolumn, out Column column)
        {
            string xml = xcolumn.ToString();
            column = null;
            XElement xname, xtype, xnullable;
            try
            {
                xname = xcolumn.Element(ns + "name");
                xtype = xcolumn.Element(ns + "type");
                xnullable = xcolumn.Element(ns + "nullable");
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

            // parse type
            string stype = xtype.Value.ToUpper();

            int[] param;
            // Text / string / hexadecimal types
            if ((stype.StartsWith("CHARACTER VARYING") && ParseParam(1, stype.Substring(16), out param)) ||
                (stype.StartsWith("VARCHAR") && ParseParam(1, stype.Substring(7), out param)))
                column = new Column(name, DataType.CHARACTER_VARYING, nullable, param);
            else if ((stype.StartsWith("CHARACTER") && ParseParam(1, stype.Substring(8), out param)) ||
                     (stype.StartsWith("CHAR") && ParseParam(1, stype.Substring(4), out param)))
                column = new Column(name, DataType.CHARACTER, nullable, param);
            else if ((stype.StartsWith("NATIONAL CHARACTER VARYING") && ParseParam(1, stype.Substring(26), out param)) ||
                     (stype.StartsWith("NATIONAL VARCHAR") && ParseParam(1, stype.Substring(16), out param)) ||
                     (stype.StartsWith("NVARCHAR") && ParseParam(1, stype.Substring(8), out param)))
                column = new Column(name, DataType.NATIONAL_CHARACTER_VARYING, nullable, param);
            else if ((stype.StartsWith("NATIONAL CHARACTER") && ParseParam(1, stype.Substring(18), out param)) ||
                     (stype.StartsWith("NATIONAL CHAR") && ParseParam(1, stype.Substring(13), out param)) ||
                     (stype.StartsWith("NCHAR") && ParseParam(1, stype.Substring(5), out param)))
                column = new Column(name, DataType.NATIONAL_CHARACTER, nullable, param);
            // Integer types
            else if (stype.StartsWith("INTEGER"))
                column = new Column(name, DataType.INTEGER, nullable, null);
            else if (stype.StartsWith("SMALL INTEGER"))
                column = new Column(name, DataType.SMALL_INTEGER, nullable, null);
            // Decimal types
            else if (stype.StartsWith("NUMERIC") && ParseParam(1, 2, stype.Substring(7), out param))
                column = new Column(name, DataType.NUMERIC, nullable, param);
            else if (stype.StartsWith("DECIMAL") && ParseParam(1, 2, stype.Substring(7), out param))
                column = new Column(name, DataType.DECIMAL, nullable, param);
            else if (stype.StartsWith("FLOAT") && ParseParam(1, stype.Substring(5), out param))
                column = new Column(name, DataType.FLOAT, nullable, param);
            else if (stype.StartsWith("DOUBLE PRECISION") && ParseParam(1, stype.Substring(16), out param))
                column = new Column(name, DataType.DOUBLE_PRECISION, nullable, param);
            else if (stype.StartsWith("REAL") && ParseParam(1, 2, stype.Substring(4), out param))
                column = new Column(name, DataType.REAL, nullable, param);
            // Boolean types
            else if (stype.StartsWith("BOOLEAN"))
                column = new Column(name, DataType.BOOLEAN, nullable, null);
            // Date / time types
            else if (stype.StartsWith("DATE"))
                column = new Column(name, DataType.DATE, nullable, null);
            else if (stype.StartsWith("TIMESTAMP"))
                column = new Column(name, DataType.TIMESTAMP, nullable, null);
            else if (stype.StartsWith("TIME"))
                column = new Column(name, DataType.TIME, nullable, null);
            //TIME_TIMEZONE
            //TIMESTAMP_TIMEZONE
            else if (stype.StartsWith("INTERVAL") && ParseParam(1, stype.Substring(16), out param))
                column = new Column(name, DataType.CHARACTER_VARYING, nullable, param);

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
