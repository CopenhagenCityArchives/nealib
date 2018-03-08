using HardHorn.Archiving;
using System;
using System.Collections.Generic;

namespace HardHorn.Analysis
{
    public class ColumnAnalysis
    {
        Dictionary<Test, List<Post>> _errorPostCaches = new Dictionary<Test, List<Post>>();
        DateTime _lastErrorsEventTime = DateTime.Now;

        public int ErrorCount { get; private set; }
        public List<Test> Tests { get; private set; }
        public Parameter MinParam { get; private set; }
        public Parameter MaxParam { get; private set; }
        public ParameterizedDataType SuggestedType { get; set; }
        public Column Column { get; private set; }
        public bool FirstRowAnalyzed { get; set; }

        public ColumnAnalysis(Column column)
        {
            FirstRowAnalyzed = false;
            Column = column;
            ErrorCount = 0;
            MinParam = null;
            MaxParam = null;
            Tests = new List<Test>();
        }

        public void ApplySuggestion()
        {
            if (SuggestedType != null)
            {
                Column.ParameterizedDataType.DataType = SuggestedType.DataType;
                Column.ParameterizedDataType.Parameter = SuggestedType.Parameter;
            }
        }

        public void RunTests(Post post)
        {
            foreach (var test in Tests)
            {
                var result = test.Run(post, Column);
                if (result == Test.Result.ERROR)
                {
                    ErrorCount++;

                    if (!_errorPostCaches.ContainsKey(test))
                    {
                        _errorPostCaches.Add(test, new List<Post>());
                    }
                }
            }
        }

        /// <summary>
        /// Update the length measurements given the new data.
        /// </summary>
        /// <param name="data"></param>
        public void UpdateLengthStatistics(string data)
        {
            switch (Column.ParameterizedDataType.DataType)
            {
                case DataType.NATIONAL_CHARACTER:
                case DataType.CHARACTER:
                case DataType.NATIONAL_CHARACTER_VARYING:
                case DataType.CHARACTER_VARYING:
                    if (FirstRowAnalyzed)
                    {
                        MinParam.Length = (uint)Math.Min(MinParam.Length, data.Length);
                        MaxParam.Length = (uint)Math.Max(MaxParam.Length, data.Length);
                    }
                    else
                    {
                        MinParam = Parameter.WithLength((uint)data.Length);
                        MaxParam = Parameter.WithLength((uint)data.Length);
                    }
                    break;
                case DataType.DECIMAL:
                    {
                        var components = data.Split('.');
                        if (components.Length > 0 && components[0].Length > 0 && components[0][0] == '-')
                        {
                            components[0] = components[0].Substring(1);
                        }
                        if (components.Length == 1)
                        {
                            components = new string[] { components[0], "" };
                        }
                        if (FirstRowAnalyzed)
                        {
                            MinParam.Precision = (uint)Math.Min(MinParam.Precision, components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                            MinParam.Scale = (uint)Math.Min(MinParam.Scale, components.Length == 1 ? 0 : components[1].Length);
                            MaxParam.Precision = (uint)Math.Max(MaxParam.Precision, components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                            MaxParam.Scale = (uint)Math.Max(MaxParam.Scale, components.Length == 1 ? 0 : components[1].Length);
                        }
                        else
                        {
                            MinParam = Parameter.WithPrecisionAndScale((uint)(components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length), (uint)(components.Length == 1 ? 0 : components[1].Length));
                            MaxParam = Parameter.WithPrecisionAndScale((uint)(components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length), (uint)(components.Length == 1 ? 0 : components[1].Length));
                        }
                    }
                    break;
                case DataType.TIME:
                case DataType.TIMESTAMP:
                    {
                        var components = data.Split('.');
                        if (components.Length == 1)
                        {
                            components = new string[] { components[0], "" };
                        }
                        if (FirstRowAnalyzed)
                        {
                            MinParam.Precision = (uint)Math.Min(MinParam.Precision, components.Length == 1 ? 0 : components[1].Length);
                            MaxParam.Precision = (uint)Math.Max(MinParam.Precision, components.Length == 1 ? 0 : components[1].Length);
                        }
                        else
                        {
                            MinParam = Parameter.WithPrecision((uint)(components.Length == 1 ? 0 : components[1].Length));
                            MaxParam = Parameter.WithPrecision((uint)(components.Length == 1 ? 0 : components[1].Length));
                        }
                    }
                    break;
                case DataType.TIME_WITH_TIME_ZONE:
                case DataType.TIMESTAMP_WITH_TIME_ZONE:
                    {
                        // Remove time zone info
                        var components = data.Split('+');
                        if (components.Length == 1)
                        {
                            components = data.Split('Z');
                        }

                        // isolate fractional part
                        components = components[0].Split('.');
                        if (components.Length == 1)
                        {
                            components = new string[] { components[0], "" };
                        }
                        if (FirstRowAnalyzed)
                        {
                            MinParam.Precision = (uint)Math.Min(MinParam.Precision, components.Length == 1 ? 0 : components[1].Length);
                            MaxParam.Precision = (uint)Math.Max(MinParam.Precision, components.Length == 1 ? 0 : components[1].Length);
                        }
                        else
                        {
                            MinParam = Parameter.WithPrecision((uint)(components.Length == 1 ? 0 : components[1].Length));
                            MaxParam = Parameter.WithPrecision((uint)(components.Length == 1 ? 0 : components[1].Length));
                        }
                    }
                    break;
            }
        }

        public void SuggestType()
        {
            switch (Column.ParameterizedDataType.DataType)
            {
                case DataType.CHARACTER:
                case DataType.CHARACTER_VARYING:
                    if (MinParam.Length == MaxParam.Length && Column.ParameterizedDataType.Parameter.Length != MaxParam.Length)
                    {
                        SuggestedType = new ParameterizedDataType(DataType.CHARACTER, Parameter.WithLength(MaxParam.Length));
                    }
                    else if (MinParam.Length != MaxParam.Length)
                    {
                        SuggestedType = new ParameterizedDataType(DataType.CHARACTER_VARYING, Parameter.WithLength(MaxParam.Length));
                    }
                    break;
                case DataType.NATIONAL_CHARACTER:
                case DataType.NATIONAL_CHARACTER_VARYING:
                    if (MinParam.Length == MaxParam.Length && Column.ParameterizedDataType.Parameter.Length != MaxParam.Length)
                    {
                        SuggestedType = new ParameterizedDataType(DataType.NATIONAL_CHARACTER, Parameter.WithLength(MaxParam.Length));
                    }
                    else if (MinParam.Length != MaxParam.Length)
                    {
                        SuggestedType = new ParameterizedDataType(DataType.NATIONAL_CHARACTER_VARYING, Parameter.WithLength(MaxParam.Length));
                    }
                    break;
                case DataType.DECIMAL:
                    if (MaxParam.Precision != Column.ParameterizedDataType.Parameter.Precision || MaxParam.Scale != Column.ParameterizedDataType.Parameter.Scale)
                    {
                        if (MaxParam.Scale == 0)
                        {
                            SuggestedType = new ParameterizedDataType(DataType.INTEGER, null);
                        }
                        else
                        {
                            SuggestedType = new ParameterizedDataType(DataType.DECIMAL, Parameter.WithPrecisionAndScale(MaxParam.Precision, MaxParam.Scale));
                        }
                    }
                    break;
                case DataType.TIME:
                    if (MaxParam.Precision != Column.ParameterizedDataType.Parameter.Precision)
                    {
                        SuggestedType = new ParameterizedDataType(DataType.TIME, Parameter.WithPrecision(MaxParam.Precision));
                    }
                    break;
                case DataType.TIMESTAMP:
                    if (MaxParam.Precision != Column.ParameterizedDataType.Parameter.Precision)
                    {
                        SuggestedType = new ParameterizedDataType(DataType.TIMESTAMP, Parameter.WithPrecision(MaxParam.Precision));
                    }
                    break;
                case DataType.TIME_WITH_TIME_ZONE:
                    if (MaxParam.Precision != Column.ParameterizedDataType.Parameter.Precision)
                    {
                        SuggestedType = new ParameterizedDataType(DataType.TIME_WITH_TIME_ZONE, Parameter.WithPrecision(MaxParam.Precision));
                    }
                    break;
                case DataType.TIMESTAMP_WITH_TIME_ZONE:
                    if (MaxParam.Precision != Column.ParameterizedDataType.Parameter.Precision)
                    {
                        SuggestedType = new ParameterizedDataType(DataType.TIMESTAMP_WITH_TIME_ZONE, Parameter.WithPrecision(MaxParam.Precision));
                    }
                    break;
            }
        }

        public void Clear()
        {
            ErrorCount = 0;
            Tests.Clear();
            _errorPostCaches.Clear();
        }
    }
}
