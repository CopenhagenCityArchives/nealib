using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace HardHorn.Statistics
{
    public class DataStatistics
    {
        public Dictionary<DataType, dynamic> DataTypeStatistics { get; private set; }

        public DataStatistics(params Table[] tables)
        {
            DataTypeStatistics = new Dictionary<DataType, dynamic>();

            foreach (var table in tables)
            {
                foreach (var column in table.Columns)
                {
                    if (DataTypeStatistics.ContainsKey(column.ParameterizedDataType.DataType))
                    {
                        DataTypeStatistics[column.ParameterizedDataType.DataType].Count++;
                        for (int i = 0; column.ParameterizedDataType.Parameter != null && i < column.ParameterizedDataType.Parameter.Length; i++)
                        {
                            if (DataTypeStatistics[column.ParameterizedDataType.DataType].MinParams[i] > column.ParameterizedDataType.Parameter[i])
                            {
                                DataTypeStatistics[column.ParameterizedDataType.DataType].MinParams[i] = column.ParameterizedDataType.Parameter[i];
                            }
                            if (DataTypeStatistics[column.ParameterizedDataType.DataType].MaxParams[i] < column.ParameterizedDataType.Parameter[i])
                            {
                                DataTypeStatistics[column.ParameterizedDataType.DataType].MaxParams[i] = column.ParameterizedDataType.Parameter[i];
                            }
                            DataTypeStatistics[column.ParameterizedDataType.DataType].ParamValues[i].Add(column.ParameterizedDataType.Parameter[i]);
                        }
                    }
                    else
                    {
                        dynamic dataTypeStat = new ExpandoObject();
                        dataTypeStat.Count = 1;
                        dataTypeStat.MaxParams = column.ParameterizedDataType.Parameter == null ? null : new Parameter(new int[column.ParameterizedDataType.Parameter.Length]);
                        dataTypeStat.MinParams = column.ParameterizedDataType.Parameter == null ? null : new Parameter(new int[column.ParameterizedDataType.Parameter.Length]);
                        if (column.ParameterizedDataType.Parameter != null && column.ParameterizedDataType.Parameter.Length > 0)
                        {
                            dataTypeStat.ParamValues = new List<int>[column.ParameterizedDataType.Parameter.Length];
                        } else
                        {
                            dataTypeStat.ParamValues = new List<int>[0];
                        }
                        for (int i = 0; i < (column.ParameterizedDataType.Parameter == null ? 0 : column.ParameterizedDataType.Parameter.Length); i++)
                        {
                            dataTypeStat.MaxParams[i] = column.ParameterizedDataType.Parameter[i];
                            dataTypeStat.MinParams[i] = column.ParameterizedDataType.Parameter[i];
                            dataTypeStat.ParamValues[i] = new List<int>();
                            dataTypeStat.ParamValues[i].Add(column.ParameterizedDataType.Parameter[i]);
                        }
                        DataTypeStatistics.Add(column.ParameterizedDataType.DataType, dataTypeStat);
                    }
                }
            }
        }
    }
}
