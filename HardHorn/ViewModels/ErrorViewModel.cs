using Caliburn.Micro;
using HardHorn.Analysis;
using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.ViewModels
{
    abstract class ErrorViewModelBase : PropertyChangedBase
    {
        public abstract string Header { get; }

        public abstract void Add(object info);
        public ObservableCollection<object> Subjects { get; set; }

        int _count = 0;
        public int Count { get { return _count; } set { _count = value; NotifyOfPropertyChange("Count"); } }

        protected ErrorViewModelBase()
        {
            Subjects = new ObservableCollection<object>();
        }
    }

    class TestErrorViewModel : ErrorViewModelBase
    {
        public override string Header
        {
            get
            {
                return TestType.ToString();
            }
        }

        public override void Add(object info)
        {
            var ea = info as AnalysisErrorOccuredArgs;
            
            if (ea == null)
            {
                throw new InvalidOperationException("Added invalid object to TestErrorViewModel.");
            }

            Count++;
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
}
