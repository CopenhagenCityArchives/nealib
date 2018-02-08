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

        public ColumnViewModel(Column column, ColumnAnalysis analysis = null)
        {
            Column = column;
            Analysis = analysis;
        }

        public void AddParameter()
        {
            if (Column.ParameterizedDataType.Parameter == null)
            {
                Column.ParameterizedDataType.Parameter = new Archiving.Parameter(new int[0]);
            }
            Column.ParameterizedDataType.AddParameterItem(0);
        }

        public void RemoveParameter()
        {
            if (Column.ParameterizedDataType.Parameter == null)
                return;
            if (Column.ParameterizedDataType.Parameter.Count == 1)
            {
                Column.ParameterizedDataType.Parameter = null;
                return;
            }
            if (Column.ParameterizedDataType.Parameter.Count > 1)
                Column.ParameterizedDataType.RemoveParameterItem(0);
        }
    }
}
