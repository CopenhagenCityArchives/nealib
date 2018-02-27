using Caliburn.Micro;
using HardHorn.Analysis;
using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HardHorn.ViewModels
{
    public abstract class ErrorViewModelBase : PropertyChangedBase
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

    public class TestErrorViewModel : ErrorViewModelBase
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
            var ccount = info as ColumnCount;

            if (ccount == null)
            {
                throw new InvalidOperationException("Added invalid object to TestErrorViewModel.");
            }

            if (_subjectIndex.ContainsKey(ccount.Column))
            {
                _subjectIndex[ccount.Column].Count = ccount.Count;
            }
            else
            {
                _subjectIndex[ccount.Column] = ccount;
                Subjects.Add(_subjectIndex[ccount.Column]);
            }

            // Re-count total errors
            Count = Subjects.Aggregate(0, (n, c) => n + (c as ColumnCount).Count);
        }

        public AnalysisTestType TestType { get; set; }

        public TestErrorViewModel(AnalysisTestType testType) : base()
        {
            TestType = testType;
        }
    }

    public class ColumnTypeParsingErrorViewModel : ErrorViewModelBase
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

    public class ColumnParsingErrorViewModel : ErrorViewModelBase
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

    public class XmlValidationErrorViewModel : ErrorViewModelBase
    {
        public override string Header { get { return "XML-Valideringsfejl"; } }

        public override void Add(object error)
        {
            var ex = error as ArchiveVersionXmlValidationException;

            if (ex == null)
            {
                throw new InvalidOperationException("Added invalid object to XmlValidationErrorViewModel.");
            }

            Count++;
            Subjects.Add(ex);
        }
    }

    public class TableRowCountViewModel : PropertyChangedBase
    {
        public Table Table { get; set; }
        public int Count { get; set; }
    }

    public class TableRowCountErrorViewModel : ErrorViewModelBase
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
