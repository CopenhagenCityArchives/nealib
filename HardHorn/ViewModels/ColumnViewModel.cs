using Caliburn.Micro;
using HardHorn.Analysis;
using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.ViewModels
{
    public class ColumnViewModel : PropertyChangedBase
    {
        public Column Column { get; private set; }

        public ColumnAnalysis Analysis { get; set; }

        public IEnumerable<Test> Tests
        {
            get { return Analysis == null ? Enumerable.Empty<Test>() : Analysis.Tests; }
        }

        public Test SelectedTest
        {
            get; set;
        }

        public int? ErrorCount
        { get { return Analysis == null ? null : new int?(Analysis.ErrorCount); }
        }

        public ParameterizedDataType ParameterizedDataType
        {
            get { return Column.ParameterizedDataType; }
            set { Column.ParameterizedDataType = value; NotifyOfPropertyChange("ParameterizedDataType"); }
        }

        public DataType DataType
        {
            get { return ParameterizedDataType.DataType; }
            set { ParameterizedDataType.DataType = value;  NotifyOfPropertyChange("DataType"); }
        }

        public Archiving.Parameter Parameter
        {
            get { return ParameterizedDataType.Parameter; }
            set
            {
                ParameterizedDataType.Parameter = value;
                NotifyOfPropertyChange("Parameter");
                NotifyOfPropertyChange("ParameterizedDataType");
                if (Parameter != null)
                    foreach (var paramValue in Parameter)
                        paramValue.PropertyChanged += OnParameterValueChanged;
            }
        }

        public string ParameterString
        {
            get
            {
                if (Parameter != null && Parameter.Count > 0)
                {
                    return "(" + string.Join(", ", Parameter.Select(pItem => pItem.Value.ToString())) + ")";
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        public ColumnViewModel(Column column, ColumnAnalysis analysis = null)
        {
            Column = column;
            Analysis = analysis;
            if (Parameter != null)
                foreach (var paramValue in Parameter)
                        paramValue.PropertyChanged += OnParameterValueChanged;
        }

        public void OnParameterValueChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            NotifyOfPropertyChange("ParameterizedDataType");
        }
    }
}
