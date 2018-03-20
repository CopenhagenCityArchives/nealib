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

        public string Description
        {
            get { return Column.Description; }
            set { Column.Description = value; NotifyOfPropertyChange("Description"); }
        }

        public ParameterizedDataType ParameterizedDataType
        {
            get { return Column.ParameterizedDataType; }
            set { Column.ParameterizedDataType = value; NotifyOfPropertyChange("ParameterizedDataType"); }
        }

        public DataType DataType
        {
            get { return ParameterizedDataType.DataType; }
            set
            {
                ParameterizedDataType.DataType = value;
                ParameterViewModel = new ParameterViewModel(Archiving.Parameter.Default(value, ParameterViewModel.Parameter));
                ParameterizedDataType.Parameter = ParameterViewModel.Parameter;
                NotifyOfPropertyChange("ParameterViewModel");
                NotifyOfPropertyChange("DataType");
            }
        }

        public ParameterViewModel ParameterViewModel
        {
            get; set;
        }

        public uint? MinLength { get { return Analysis == null || Analysis.CharacterMinParameter == null ? null : (uint?)Analysis.CharacterMinParameter.Length; } }
        public uint? MaxLength { get { return Analysis == null || Analysis.CharacterMaxParameter == null ? null : (uint?)Analysis.CharacterMaxParameter.Length; } }

        public ColumnViewModel(Column column, ColumnAnalysis analysis = null)
        {
            Column = column;
            Analysis = analysis;
            ParameterViewModel = new ParameterViewModel(column.ParameterizedDataType.Parameter);
        }
    }
}
