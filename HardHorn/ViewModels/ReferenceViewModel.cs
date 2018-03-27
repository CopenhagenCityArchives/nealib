using Caliburn.Micro;
using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.ViewModels
{
    public class ReferenceViewModel : PropertyChangedBase
    {
        public Reference Reference { get; private set; }

        public ReferenceViewModel(Reference reference)
        {
            Reference = reference;
        }
    }
}
