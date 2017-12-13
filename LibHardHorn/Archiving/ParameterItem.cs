using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HardHorn.Utility;

namespace HardHorn.Archiving
{
    public class ParameterItem : NotifyPropertyChangedBase, IComparable<ParameterItem>
    {
        int _value;
        public int Value
        {
            get { return _value; }
            set { _value = value; NotifyOfPropertyChanged("Value"); }
        }

        public ParameterItem(int value)
        {
            Value = value;
        }

        public int CompareTo(ParameterItem other) {
            return Value.CompareTo(other.Value);
        }
    }
}
