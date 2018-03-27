using Caliburn.Micro;
using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.ViewModels
{
    public class ForeignKeyViewModel : PropertyChangedBase
    {
        public TableViewModel TableViewModel { get; private set; }
        public ForeignKey ForeignKey { get; private set; }
        public ObservableCollection<ReferenceViewModel> ReferenceViewModels { get; private set; }
        public string Name
        {
            get { return ForeignKey.Name; }
            set { ForeignKey.Name = value; NotifyOfPropertyChange("Name"); }
        }

        public ForeignKeyViewModel(TableViewModel tableViewModel, ForeignKey foreignKey)
        {
            ForeignKey = foreignKey;
            TableViewModel = tableViewModel;
            ReferenceViewModels = new ObservableCollection<ReferenceViewModel>(foreignKey.References.Select(reference => new ReferenceViewModel(reference)));
        }
    }
}
