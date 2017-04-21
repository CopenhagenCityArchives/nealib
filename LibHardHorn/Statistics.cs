using HardHorn.ArchiveVersion;
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
                    if (DataTypeStatistics.ContainsKey(column.Type))
                    {
                        DataTypeStatistics[column.Type].Count++;
                        for (int i = 0; column.Param != null && i < column.Param.Length; i++)
                        {
                            if (DataTypeStatistics[column.Type].MinParams[i] > column.Param[i])
                            {
                                DataTypeStatistics[column.Type].MinParams[i] = column.Param[i];
                            }
                            if (DataTypeStatistics[column.Type].MaxParams[i] < column.Param[i])
                            {
                                DataTypeStatistics[column.Type].MaxParams[i] = column.Param[i];
                            }
                        }
                    }
                    else
                    {
                        dynamic dataTypeStat = new ExpandoObject();
                        dataTypeStat.Count = 1;
                        dataTypeStat.MaxParams = column.Param == null ? null : new DataTypeParam(new int[column.Param.Length]);
                        dataTypeStat.MinParams = column.Param == null ? null : new DataTypeParam(new int[column.Param.Length]);
                        for (int i = 0; i < (column.Param == null ? 0 : column.Param.Length); i++)
                        {
                            dataTypeStat.MaxParams[i] = column.Param[i];
                            dataTypeStat.MinParams[i] = column.Param[i];
                        }
                        DataTypeStatistics.Add(column.Type, dataTypeStat);
                    }
                }
            }
        }
    }
}
