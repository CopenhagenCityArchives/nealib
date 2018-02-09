using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace HardHorn.Statistics
{
    public class BarChartConfiguration
    {
        public int BucketCount { get; set; }
        public IList<int> Values { get; set; }

        public BarChartConfiguration(int bCount, IList<int> values)
        {
            BucketCount = bCount;
            Values = values;
        }
    }

    public class DataTypeStatistic
    {
        public int Count { get; set; }
        public Parameter MinParams { get; set; }
        public Parameter MaxParams { get; set; }
        public IList<int>[] ParamValues { get; set; }
        public IList<BarChartConfiguration> BarCharts { get; set; }

        public DataTypeStatistic(DataType dataType)
        {
            Count = 0;

            int paramCount = 0;
            switch (dataType)
            {
                case DataType.CHARACTER:
                case DataType.CHARACTER_VARYING:
                case DataType.NATIONAL_CHARACTER:
                case DataType.NATIONAL_CHARACTER_VARYING:
                case DataType.TIME:
                case DataType.TIME_WITH_TIME_ZONE:
                case DataType.TIMESTAMP:
                case DataType.TIMESTAMP_WITH_TIME_ZONE:
                case DataType.REAL:
                case DataType.FLOAT:
                    paramCount = 1;
                    break;
                case DataType.DECIMAL:
                case DataType.NUMERIC:
                    paramCount = 2;
                    break;
            }
            MinParams = new Parameter(new int[paramCount]);
            MaxParams = new Parameter(new int[paramCount]);
            ParamValues = new IList<int>[paramCount];
            BarCharts = new List<BarChartConfiguration>();
            for (int i = 0; i < paramCount; i++)
            {
                ParamValues[i] = new List<int>();
                BarCharts.Add(new BarChartConfiguration(10, ParamValues[i]));
            }
        }
    }


    public class DataStatistics
    {
        public Dictionary<DataType, DataTypeStatistic> DataTypeStatistics { get; private set; }

        public DataStatistics(params Table[] tables)
        {
            DataTypeStatistics = new Dictionary<DataType, DataTypeStatistic>();

            foreach (var table in tables)
            {
                foreach (var column in table.Columns)
                {
                    if (DataTypeStatistics.ContainsKey(column.ParameterizedDataType.DataType))
                    {
                        DataTypeStatistics[column.ParameterizedDataType.DataType].Count++;
                        for (int i = 0; column.ParameterizedDataType.Parameter != null && i < column.ParameterizedDataType.Parameter.Count; i++)
                        {
                            if (DataTypeStatistics[column.ParameterizedDataType.DataType].MinParams[i].Value > column.ParameterizedDataType.Parameter[i].Value)
                            {
                                DataTypeStatistics[column.ParameterizedDataType.DataType].MinParams[i].Value = column.ParameterizedDataType.Parameter[i].Value;
                            }
                            if (DataTypeStatistics[column.ParameterizedDataType.DataType].MaxParams[i].Value < column.ParameterizedDataType.Parameter[i].Value)
                            {
                                DataTypeStatistics[column.ParameterizedDataType.DataType].MaxParams[i].Value = column.ParameterizedDataType.Parameter[i].Value;
                            }
                            DataTypeStatistics[column.ParameterizedDataType.DataType].ParamValues[i].Add(column.ParameterizedDataType.Parameter[i].Value);
                        }
                    }
                    else
                    {
                        DataTypeStatistic dataTypeStat = new DataTypeStatistic(column.ParameterizedDataType.DataType);
                        dataTypeStat.Count = 1;
                        for (int i = 0; i < (column.ParameterizedDataType.Parameter == null ? 0 : column.ParameterizedDataType.Parameter.Count); i++)
                        {
                            dataTypeStat.MaxParams[i].Value = column.ParameterizedDataType.Parameter[i].Value;
                            dataTypeStat.MinParams[i].Value = column.ParameterizedDataType.Parameter[i].Value;
                            dataTypeStat.ParamValues[i].Add(column.ParameterizedDataType.Parameter[i].Value);
                        }
                        DataTypeStatistics.Add(column.ParameterizedDataType.DataType, dataTypeStat);
                    }
                }
            }
        }
    }
}
