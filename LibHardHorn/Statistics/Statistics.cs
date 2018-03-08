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
        public IList<uint> Values { get; set; }

        public BarChartConfiguration(int bCount, IList<uint> values)
        {
            BucketCount = bCount;
            Values = values;
        }
    }

    public class DataTypeStatistic
    {
        public int Count { get; set; }
        public Parameter MinParam { get; set; }
        public Parameter MaxParam { get; set; }
        public IList<Parameter> ParamValues { get; set; }

        public IEnumerable<BarChartConfiguration> BarCharts
        {
            get
            {
                var scales = new List<uint>();
                var precisions = new List<uint>();
                var length = new List<uint>();

                foreach (var pvalue in ParamValues)
                {
                    if (pvalue.HasScale)
                        scales.Add(pvalue.Scale);
                    if (pvalue.HasPrecision)
                        scales.Add(pvalue.Precision);
                    if (pvalue.HasLength)
                        scales.Add(pvalue.Length);
                }

                if (scales.Count > 0)
                    yield return new BarChartConfiguration(10, scales);

                if (scales.Count > 0)
                    yield return new BarChartConfiguration(10, precisions);

                if (scales.Count > 0)
                    yield return new BarChartConfiguration(10, length);
            }
        }

        public DataTypeStatistic(DataType dataType)
        {
            Count = 0;
            ParamValues = new List<Parameter>();
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
                        var dataTypeStat = DataTypeStatistics[column.ParameterizedDataType.DataType];
                        if (column.ParameterizedDataType.Parameter == null) { }
                        else if (column.ParameterizedDataType.Parameter.HasLength)
                        {
                            dataTypeStat.MinParam.Length = Math.Min(dataTypeStat.MinParam.Length, column.ParameterizedDataType.Parameter.Length);
                            dataTypeStat.MaxParam.Length = Math.Max(dataTypeStat.MinParam.Length, column.ParameterizedDataType.Parameter.Length);
                        }
                        else if (column.ParameterizedDataType.Parameter.HasPrecision && column.ParameterizedDataType.Parameter.HasScale)
                        {
                            dataTypeStat.MinParam = Parameter.WithPrecisionAndScale(
                                Math.Min(dataTypeStat.MinParam.Precision, column.ParameterizedDataType.Parameter.Precision),
                                Math.Min(dataTypeStat.MinParam.Scale, column.ParameterizedDataType.Parameter.Scale));

                            dataTypeStat.MaxParam = Parameter.WithPrecisionAndScale(
                                 Math.Max(dataTypeStat.MinParam.Precision, column.ParameterizedDataType.Parameter.Precision),
                                 Math.Max(dataTypeStat.MinParam.Scale, column.ParameterizedDataType.Parameter.Scale));
                        }
                        else if (column.ParameterizedDataType.Parameter.HasPrecision)
                        {

                            dataTypeStat.MinParam.Precision = Math.Min(dataTypeStat.MinParam.Precision, column.ParameterizedDataType.Parameter.Precision);
                            dataTypeStat.MaxParam.Precision = Math.Max(dataTypeStat.MinParam.Precision, column.ParameterizedDataType.Parameter.Precision);
                        }

                        dataTypeStat.Count++;
                    }
                    else
                    {
                        var dataTypeStat = new DataTypeStatistic(column.ParameterizedDataType.DataType);
                        dataTypeStat.Count = 1;

                        if (column.ParameterizedDataType.Parameter == null) { }
                        else if (column.ParameterizedDataType.Parameter.HasLength)
                        {
                            dataTypeStat.MinParam = Parameter.WithLength(column.ParameterizedDataType.Parameter.Length);
                            dataTypeStat.MaxParam = Parameter.WithLength(column.ParameterizedDataType.Parameter.Length);
                        }
                        else if (column.ParameterizedDataType.Parameter.HasPrecision && column.ParameterizedDataType.Parameter.HasScale)
                        {
                            dataTypeStat.MinParam = Parameter.WithPrecisionAndScale(column.ParameterizedDataType.Parameter.Precision, column.ParameterizedDataType.Parameter.Scale);
                            dataTypeStat.MaxParam = Parameter.WithPrecisionAndScale(column.ParameterizedDataType.Parameter.Precision, column.ParameterizedDataType.Parameter.Scale);
                        }
                        else if (column.ParameterizedDataType.Parameter.HasPrecision)
                        {
                            dataTypeStat.MinParam = Parameter.WithPrecision(column.ParameterizedDataType.Parameter.Precision);
                            dataTypeStat.MaxParam = Parameter.WithPrecision(column.ParameterizedDataType.Parameter.Precision);
                        }

                        DataTypeStatistics.Add(column.ParameterizedDataType.DataType, dataTypeStat);
                    }
                }
            }
        }
    }
}
