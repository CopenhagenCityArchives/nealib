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

        public static DataType Parse(string dataType, out bool illegalAlias)
        {
            var upperDataType = dataType.ToUpper();
            illegalAlias = false;

            switch (upperDataType)
            {
                case "CHARACTER VARYING":
                case "VARCHAR":
                    return DataType.CHARACTER_VARYING;
                case "CHAR VARYING":
                    illegalAlias = true;
                    return DataType.CHARACTER_VARYING;
                case "CHARACTER":
                case "CHAR":
                    return DataType.CHARACTER;
                case "NATIONAL CHARACTER VARYING":
                case "NATIONAL VARCHAR":
                case "NVARCHAR":
                    return DataType.NATIONAL_CHARACTER_VARYING;
                case "NATIONAL CHARACTER":
                case "NATIONAL CHAR":
                case "NCHAR":
                    return DataType.NATIONAL_CHARACTER;
                case "NATIONAL CHAR VARYING":
                case "NCHAR VARYING":
                    illegalAlias = true;
                    return DataType.NATIONAL_CHARACTER_VARYING;
                // Integer types
                case "INTEGER":
                    return DataType.INTEGER;
                case "INT":
                    illegalAlias = true;
                    return DataType.INTEGER;
                case "SMALLINT":
                    return DataType.SMALLINT;
                // Decimal types
                case "NUMERIC":
                    return DataType.NUMERIC;
                case "DECIMAL":
                    return DataType.DECIMAL;
                case "DEC":
                    illegalAlias = true;
                    return DataType.DECIMAL;
                case "FLOAT":
                    return DataType.FLOAT;
                case "DOUBLE PRECISION":
                    return DataType.DOUBLE_PRECISION;
                case "REAL":
                    return DataType.REAL;
                // Boolean types
                case "BOOLEAN":
                    return DataType.BOOLEAN;
                // Date / time types
                case "DATE":
                    return DataType.DATE;
                case "TIMESTAMP WITH TIME ZONE":
                    return DataType.TIMESTAMP_WITH_TIME_ZONE;
                case "TIMESTAMP WITHOUT TIME ZONE":
                    return DataType.TIMESTAMP;
                case "TIMESTAMP":
                    return DataType.TIMESTAMP;
                case "TIME WITHOUT TIME ZONE":
                    return DataType.TIME;
                case "TIME WITH TIME ZONE":
                    return DataType.TIME_WITH_TIME_ZONE;
                case "TIME":
                    return DataType.TIME;
                case "INTERVAL":
                    return DataType.CHARACTER_VARYING;
            }

            var exp = new InvalidOperationException("Could not parse data type '"+dataType+"'.");
            exp.Data["DataType"] = dataType;
            throw exp;
        }

        public static bool IsParameterValidFor(DataType dataType, Parameter parameter)
        {
            switch (dataType)
            {
                case DataType.CHARACTER:
                case DataType.CHARACTER_VARYING:
                case DataType.NATIONAL_CHARACTER:
                case DataType.NATIONAL_CHARACTER_VARYING:
                    return parameter.HasLength;
                case DataType.TIMESTAMP:
                case DataType.TIMESTAMP_WITH_TIME_ZONE:
                case DataType.TIME:
                case DataType.TIME_WITH_TIME_ZONE:
                    return parameter.HasPrecision;
                case DataType.NUMERIC:
                case DataType.DECIMAL:
                    return parameter.HasPrecision && parameter.HasScale;
                default:
                    return parameter == null || !(parameter.HasLength || parameter.HasPrecision || parameter.HasScale);
            }
        }

        public static bool ValidateParameters(DataType type, int[] parameters)
        {
            switch (type)
            {
                case DataType.CHARACTER:
                case DataType.CHARACTER_VARYING:
                case DataType.NATIONAL_CHARACTER:
                case DataType.NATIONAL_CHARACTER_VARYING:
                    return parameters == null ||parameters.Length == 0 || parameters.Length == 1;
                case DataType.TIMESTAMP:
                case DataType.TIMESTAMP_WITH_TIME_ZONE:
                case DataType.TIME:
                case DataType.TIME_WITH_TIME_ZONE:
                    return parameters == null || parameters.Length == 0 || parameters.Length == 1;
                case DataType.NUMERIC:
                case DataType.DECIMAL:
                    return parameters != null && (parameters.Length == 1 || parameters.Length == 2);
                default:
                    return parameters == null || parameters.Length == 0;
            }
        }
    }
}
