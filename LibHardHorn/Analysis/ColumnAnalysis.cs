using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.Analysis
{
    public class ColumnAnalysis : AnalysisErrorsOccuredBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        void NotifyOfPropertyChanged(string propertyName)
        {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        Test _selectedTest;
        public Test SelectedTest { get { return _selectedTest; } set { _selectedTest = Tests.IndexOf(value) == -1 ? _selectedTest : value; PropertyChanged(this, new PropertyChangedEventArgs("SelectedTest")); } }

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
            MinParam = new Parameter(new int[column.ParameterizedDataType.Parameter != null ? column.ParameterizedDataType.Parameter.Length : 1]);
            MaxParam = new Parameter(new int[column.ParameterizedDataType.Parameter != null ? column.ParameterizedDataType.Parameter.Length : 1]);
            Tests = new List<Test>();
        }

        public void ApplySuggestion()
        {
            if (SuggestedType != null)
            {
                Column.ParameterizedDataType = SuggestedType;
                NotifyOfPropertyChanged("Column.ParameterizedDataType.DataType");
                NotifyOfPropertyChanged("Column.ParameterizedDataType.Parameter");
                NotifyOfPropertyChanged("Column.ParameterizedDataType");
                NotifyOfPropertyChanged("Column");
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
                    _errorPostCaches[test].Add(post);
                    TimeSpan diff = DateTime.Now - _lastErrorsEventTime;
                    if (_errorPostCaches[test].Count == 10000 || diff.Seconds > 2)
                    {
                        NotifyOfAnalysisErrorOccured(test, new List<Post>(_errorPostCaches[test]), Column);
                        PropertyChanged(this, new PropertyChangedEventArgs("ErrorCount"));
                        _lastErrorsEventTime = DateTime.Now;
                        _errorPostCaches[test].Clear();
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
                    if (FirstRowAnalyzed)
                    {
                        MinParam[0] = Math.Min(MinParam[0], data.Length);
                        MaxParam[0] = Math.Max(MaxParam[0], data.Length);
                    }
                    else
                    {
                        MinParam[0] = data.Length;
                        MaxParam[0] = data.Length;
                    }
                    break;
                case DataType.NATIONAL_CHARACTER_VARYING:
                case DataType.CHARACTER_VARYING:
                    if (FirstRowAnalyzed)
                    {
                        MinParam[0] = Math.Min(MinParam[0], data.Length);
                        MaxParam[0] = Math.Max(MaxParam[0], data.Length);
                    }
                    else
                    {
                        MinParam[0] = data.Length;
                        MaxParam[0] = data.Length;
                    }
                    break;
                case DataType.DECIMAL:
                    var components = data.Split('.');
                    if (components.Length > 0 && components[0].Length > 0 && components[0][0] == '-')
                    {
                        components[0] = components[0].Substring(1);
                    }
                    if (FirstRowAnalyzed)
                    {
                        MinParam[0] = Math.Min(MinParam[0], components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                        MaxParam[0] = Math.Max(MaxParam[0], components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                        MinParam[1] = Math.Min(MinParam[1], components.Length == 1 ? 0 : components[1].Length);
                        MaxParam[1] = Math.Max(MaxParam[1], components.Length == 1 ? 0 : components[1].Length);
                    }
                    else
                    {
                        MinParam[0] = components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length;
                        MaxParam[0] = components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length;
                        MinParam[1] = components.Length == 1 ? 0 : components[1].Length;
                        MaxParam[1] = components.Length == 1 ? 0 : components[1].Length;
                    }
                    break;
                case DataType.TIME:
                case DataType.DATE:
                case DataType.TIMESTAMP:
                    break;
            }
        }

        public void SuggestType()
        {
            switch (Column.ParameterizedDataType.DataType)
            {
                case DataType.CHARACTER:
                    if (MinParam[0] == MaxParam[0] && MaxParam[0] > Column.ParameterizedDataType.Parameter[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.CHARACTER, new Parameter(MaxParam[0]));
                    }
                    else if (MinParam[0] != MaxParam[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.CHARACTER_VARYING, new Parameter(MaxParam[0]));
                    }
                    break;
                case DataType.NATIONAL_CHARACTER:
                    if (MinParam[0] == MaxParam[0] && MaxParam[0] > Column.ParameterizedDataType.Parameter[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.NATIONAL_CHARACTER, new Parameter(MaxParam[0]));
                    }
                    else if (MinParam[0] != MaxParam[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.NATIONAL_CHARACTER_VARYING, new Parameter(MaxParam[0]));
                    }
                    break;
                case DataType.CHARACTER_VARYING:
                    if (MinParam[0] == MaxParam[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.CHARACTER, new Parameter(MaxParam[0]));
                    }
                    else if (MaxParam[0] != Column.ParameterizedDataType.Parameter[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.CHARACTER_VARYING, new Parameter(MaxParam[0]));
                    }
                    break;
                case DataType.NATIONAL_CHARACTER_VARYING:
                    if (MinParam[0] == MaxParam[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.NATIONAL_CHARACTER, new Parameter(MaxParam[0]));
                    }
                    else if (MaxParam[0] != Column.ParameterizedDataType.Parameter[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.NATIONAL_CHARACTER_VARYING, new Parameter(MaxParam[0]));
                    }
                    break;
                case DataType.DECIMAL:
                    if (MaxParam[0] != Column.ParameterizedDataType.Parameter[0] || MaxParam[1] != Column.ParameterizedDataType.Parameter[1])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.DECIMAL, new Parameter(new int[] { MaxParam[0], MaxParam[1] }));
                    }
                    break;
            }

            NotifyOfPropertyChanged("SuggestedType");
        }

        public void Clear()
        {
            ErrorCount = 0;
            Tests.Clear();
        }

        internal void Flush()
        {
            foreach (var testCache in _errorPostCaches)
            {
                if (testCache.Value.Count > 0)
                {
                    NotifyOfAnalysisErrorOccured(testCache.Key, new List<Post>(testCache.Value), Column);
                }
            }
            NotifyOfPropertyChanged("ErrorCount");
        }
    }
}
