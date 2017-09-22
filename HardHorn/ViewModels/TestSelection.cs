using Caliburn.Micro;
using HardHorn.Archiving;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace HardHorn.ViewModels
{
    [Serializable()]
    public class DataTypeSelection : INotifyPropertyChanged
    {
        [field:NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyOfPropertyChange(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public DataType DataType { get; set; }

        public DataTypeSelection() {}

        bool? _selected = true;
        public bool? Selected
        {
            get { return _selected; }
            set { _selected = value; NotifyOfPropertyChange("Selected"); }
        }

        public DataTypeSelection(DataType dataType)
        {
            DataType = dataType;
        }
    }

    [Serializable()]
    public class TestTypeSelection : INotifyPropertyChanged, IEnumerable<DataTypeSelection>
    {
        public TestSelectionType TestType { get; set; }
        List<DataTypeSelection> _dataTypeSelections;

        [field:NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyOfPropertyChange(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        bool _settingSelected = false;
        bool? _selected = true;
        public bool? Selected
        {
            get { return _selected; }
            set
            {
                _settingSelected = true;
                _selected = value;
                foreach (var dataTypeSelection in _dataTypeSelections)
                {
                    dataTypeSelection.Selected = value;
                }
                NotifyOfPropertyChange("Selected");
                _settingSelected = false;
            }
        }

        #region IEnumerable
        public IEnumerator<DataTypeSelection> GetEnumerator()
        {
            return ((IEnumerable<DataTypeSelection>)_dataTypeSelections).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<DataTypeSelection>)_dataTypeSelections).GetEnumerator();
        }

        public void Add(DataTypeSelection dataTypeSelection)
        {
            dataTypeSelection.PropertyChanged += childSelectionChanged;
            _dataTypeSelections.Add(dataTypeSelection);
        }
        #endregion

        private void childSelectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_settingSelected)
                return;

            var dataTypeSelection = sender as DataTypeSelection;
            if (e.PropertyName == "Selected")
            {
                bool allSelected = this.All(dts => dts.Selected.HasValue && dts.Selected.Value);
                bool someSelected = this.Any(dts => dts.Selected.HasValue && dts.Selected.Value);

                if (allSelected) _selected = true;
                if (!allSelected && someSelected) _selected = null;
                if (!someSelected) _selected = false;

                NotifyOfPropertyChange("Selected");
            }
        }

        public void HookupEvents()
        {
            foreach (var child in this)
            {
                child.PropertyChanged += childSelectionChanged;
            }
        }

        public TestTypeSelection()
        {
            _dataTypeSelections = new List<DataTypeSelection>();
        }

        public TestTypeSelection(TestSelectionType testType)
        {
            TestType = testType;
            _dataTypeSelections = new List<DataTypeSelection>();
        }

    }

    [Serializable()]
    public class TestSelectionCategory : INotifyPropertyChanged, IEnumerable<TestTypeSelection>
    {
        public string Name { get; set; }
        List<TestTypeSelection> _testTypeSelections { get; set; }
        public IEnumerable<DataTypeSelection> DataTypes
        {
            get
            {
                foreach (var testType in _testTypeSelections)
                    foreach (var dataType in testType)
                        yield return dataType;
            }
        }

        bool _settingSelected = false;
        bool? _selected = true;

        [field:NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyOfPropertyChange(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public bool? Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                foreach (var testType in _testTypeSelections)
                {
                    testType.Selected = value;
                }
                NotifyOfPropertyChange("Selected");
            }
        }

        #region IEnumerable
        public IEnumerator<TestTypeSelection> GetEnumerator()
        {
            return ((IEnumerable<TestTypeSelection>)_testTypeSelections).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<TestTypeSelection>)_testTypeSelections).GetEnumerator();
        }

        public void Add(TestTypeSelection testTypeSelection)
        {
            testTypeSelection.PropertyChanged += childSelectionChanged;
            _testTypeSelections.Add(testTypeSelection);
        }
        #endregion

        private void childSelectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_settingSelected)
                return;

            var testTypeSelection = sender as TestTypeSelection;
            if (e.PropertyName == "Selected")
            {
                bool allSelected = this.All(tts => tts.Selected.HasValue && tts.Selected.Value);
                bool someSelected = this.Any(tts => tts.Selected.HasValue && tts.Selected.Value);

                if (allSelected) _selected = true;
                if (!allSelected && someSelected) _selected = null;
                if (!someSelected) _selected = false;

                NotifyOfPropertyChange("Selected");
            }
        }

        public TestSelectionCategory()
        {
            _testTypeSelections = new List<TestTypeSelection>();
        }

        public void HookupEvents()
        {
            foreach (var child in this)
            {
                child.PropertyChanged += childSelectionChanged;
                child.HookupEvents();
            }
        }

        public TestSelectionCategory(string name, DataType[] dataTypes, TestSelectionType[] testTypes)
        {
            Name = name;
            _testTypeSelections = new List<TestTypeSelection>();

            foreach (var testType in testTypes)
            {
                var testTypeSelection = new TestTypeSelection(testType);
                var dataTypeTests = new List<DataTypeSelection>();

                foreach (var dataType in dataTypes)
                {
                    testTypeSelection.Add(new DataTypeSelection(dataType));
                }
                Add(testTypeSelection);
            }
        }
    }

    [Serializable()]
    public enum TestSelectionType
    {
        UNDERFLOW, OVERFLOW, FORMAT, BLANK
    }

    [Serializable()]
    public class TestSelection : INotifyPropertyChanged, IEnumerable<TestSelectionCategory>
    {
        [field:NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyOfPropertyChange(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        List<TestSelectionCategory> _testSelectionCategories;
        public IEnumerable<DataTypeSelection> DataTypes
        {
            get
            {
                foreach (var category in _testSelectionCategories)
                    foreach (var dataType in category.DataTypes)
                        yield return dataType;
            }
        }

        public TestSelection()
        {
            _testSelectionCategories = new List<TestSelectionCategory>();
        }

        public static TestSelection GetFullSelection()
        {
            var testSelection = new TestSelection();
            testSelection.Add(new TestSelectionCategory("Strengtyper",
                new DataType[] { DataType.CHARACTER, DataType.CHARACTER_VARYING, DataType.NATIONAL_CHARACTER, DataType.NATIONAL_CHARACTER_VARYING },
                new TestSelectionType[] { TestSelectionType.OVERFLOW, TestSelectionType.UNDERFLOW, TestSelectionType.BLANK }));
            testSelection.Add(new TestSelectionCategory("Tidstyper",
                new DataType[] { DataType.TIME, DataType.TIME_WITH_TIME_ZONE, DataType.TIMESTAMP, DataType.TIMESTAMP_WITH_TIME_ZONE, DataType.DATE, DataType.INTERVAL },
                new TestSelectionType[] { TestSelectionType.OVERFLOW, TestSelectionType.FORMAT }));
            testSelection.Add(new TestSelectionCategory("Decimaltalstyper",
                new DataType[] { DataType.DECIMAL, DataType.DOUBLE_PRECISION, DataType.FLOAT, DataType.REAL },
                new TestSelectionType[] { TestSelectionType.OVERFLOW }));
            return testSelection;
        }

        public void Add(TestSelectionCategory category)
        {
            _testSelectionCategories.Add(category);
        }

        public IEnumerator<TestSelectionCategory> GetEnumerator()
        {
            return ((IEnumerable<TestSelectionCategory>)_testSelectionCategories).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<TestSelectionCategory>)_testSelectionCategories).GetEnumerator();
        }
    }
}
