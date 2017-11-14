using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.ViewModels
{
    class ErrorViewModel : PropertyChangedBase
    {
        public string Name { get; private set; }

        int _count = 0;
        public int Count { get { return _count; } set { _count = value; NotifyOfPropertyChange("Count"); } }

        public ErrorViewModel(string name)
        {
            Name = name;
        }
    }
}
