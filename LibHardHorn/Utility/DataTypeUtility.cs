using HardHorn.Archiving;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Utility
{
    public static class DataTypeUtility
    {
        public static string ToString(DataType dataType)
        {
            return dataType.ToString().Replace('_', ' ');
        }

        public static DataType Parse(string dataType)
        {
            var upperDataType = dataType.ToUpper();

            if (upperDataType.StartsWith("CHARACTER VARYING") ||
                upperDataType.StartsWith("VARCHAR"))
                return DataType.CHARACTER_VARYING;
            else if (upperDataType.StartsWith("CHARACTER") ||
                     upperDataType.StartsWith("CHAR"))
                return DataType.CHARACTER;
            else if (upperDataType.StartsWith("NATIONAL CHARACTER VARYING") ||
                     upperDataType.StartsWith("NATIONAL VARCHAR") ||
                     upperDataType.StartsWith("NVARCHAR"))
                return DataType.NATIONAL_CHARACTER_VARYING;
            else if (upperDataType.StartsWith("NATIONAL CHARACTER") ||
                     upperDataType.StartsWith("NATIONAL CHAR") ||
                     upperDataType.StartsWith("NCHAR"))
                return DataType.NATIONAL_CHARACTER;
            // Integer types
            else if (upperDataType.StartsWith("INTEGER"))
                return DataType.INTEGER;
            else if (upperDataType.StartsWith("SMALL INTEGER"))
                return DataType.SMALL_INTEGER;
            // Decimal types
            else if (upperDataType.StartsWith("NUMERIC"))
                return DataType.NUMERIC;
            else if (upperDataType.StartsWith("DECIMAL"))
                return DataType.DECIMAL;
            else if (upperDataType.StartsWith("FLOAT"))
                return DataType.FLOAT;
            else if (upperDataType.StartsWith("DOUBLE PRECISION"))
                return DataType.DOUBLE_PRECISION;
            else if (upperDataType.StartsWith("REAL"))
                return DataType.REAL;
            // Boolean types
            else if (upperDataType.StartsWith("BOOLEAN"))
                return DataType.BOOLEAN;
            // Date / time types
            else if (upperDataType.StartsWith("DATE"))
                return DataType.DATE;
            else if (upperDataType.StartsWith("TIMESTAMP WITH TIME ZONE"))
                return DataType.TIMESTAMP_WITH_TIME_ZONE;
            else if (upperDataType.StartsWith("TIMESTAMP WITHOUT TIME ZONE"))
                return DataType.TIMESTAMP;
            else if (upperDataType.StartsWith("TIMESTAMP"))
                return DataType.TIMESTAMP;
            else if (upperDataType.StartsWith("TIME WITHOUT TIME ZONE"))
                return DataType.TIME;
            else if (upperDataType.StartsWith("TIME WITH TIME ZONE"))
                return DataType.TIME_WITH_TIME_ZONE;
            else if (upperDataType.StartsWith("TIME"))
                return DataType.TIME;
            else if (upperDataType.StartsWith("INTERVAL"))
                return DataType.CHARACTER_VARYING;

            var exp = new InvalidOperationException("Could not parse data type '"+dataType+"'.");
            exp.Data["DataType"] = dataType;
            throw exp;
        }
    }
}
