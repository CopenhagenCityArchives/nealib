using Caliburn.Micro;
using HardHorn.Analysis;
using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HardHorn.ViewModels
{
    abstract class ErrorViewModelBase : PropertyChangedBase
    {
        public abstract string Header { get; }

        public abstract void Add(object info);
        public ObservableCollection<object> Subjects { get; set; }

        long _count = 0;
        public long Count { get { return _count; } set { _count = value; NotifyOfPropertyChange("Count"); } }

        protected ErrorViewModelBase()
        {
            Subjects = new ObservableCollection<object>();
        }
    }

    public class ColumnCount : PropertyChangedBase
    {
        public Column Column { get; set; }
        private int _count = 0;
        public int Count { get { return _count; } set { _count = value; NotifyOfPropertyChange("Count"); } }
    }

    class TestErrorViewModel : ErrorViewModelBase
    {
        Dictionary<Column, ColumnCount> _subjectIndex = new Dictionary<Column, ColumnCount>();

        public override string Header
        {
            get
            {
                return TestType.ToString();
            }
        }

        public override void Add(object info)
        {
            var ea = info as AnalysisErrorsOccuredArgs;
            
            if (ea == null)
            {
                throw new InvalidOperationException("Added invalid object to TestErrorViewModel.");
            }

            if (_subjectIndex.ContainsKey(ea.Column))
            {
                _subjectIndex[ea.Column].Count += ea.Posts.Count();
            }
            else
            {
                _subjectIndex[ea.Column] = new ColumnCount() { Count = ea.Posts.Count(), Column = ea.Column };
                Subjects.Add(_subjectIndex[ea.Column]);
            }

            Count += ea.Posts.Count();
        }

        public AnalysisTestType TestType { get; set; }

        public TestErrorViewModel(AnalysisTestType testType) : base()
        {
            TestType = testType;
        }
    }

    class ColumnTypeParsingErrorViewModel : ErrorViewModelBase
    {
        public override string Header
        {
            get
            {
                return "Kolonnetypefejl";
            }
        }

        public ColumnTypeParsingErrorViewModel()
        {
        }

        public override void Add(object info)
        {
            var ex = info as ArchiveVersionColumnTypeParsingException;

            if (ex == null)
            {
                throw new InvalidOperationException("Added invalid object to ColumnTypeParsingErrorViewModel.");
            }

            Count++;
            Subjects.Add(ex);
        }
    }

    class ColumnParsingErrorViewModel : ErrorViewModelBase
    {
        public override string Header
        {
            get
            {
                return "Kolonnetypefejl";
            }
        }

        public override void Add(object error)
        {
            var ex = error as ArchiveVersionColumnParsingException;

            if (ex == null)
            {
                throw new InvalidOperationException("Added invalid object to ColumnParsingErrorViewModel.");
            }

            Count++;
            Subjects.Add(ex);
        }
    }

    class TableRowCountViewModel : PropertyChangedBase
    {
        public Table Table { get; set; }
        public int Count { get; set; }
    }

    class TableRowCountErrorViewModel : ErrorViewModelBase
    {
        public override string Header
        {
            get
            {
                return "Rækkeantalsfejl";
            }
        }

        public override void Add(object info)
        {
            var tuple = info as Tuple<Table, int>;

            if (tuple == null)
            {
                throw new InvalidOperationException("Added invalid object to TableRowCountErrorViewModel");
            }

            Subjects.Add(new TableRowCountViewModel() { Table = tuple.Item1, Count = tuple.Item2 });
        }
    }
}
