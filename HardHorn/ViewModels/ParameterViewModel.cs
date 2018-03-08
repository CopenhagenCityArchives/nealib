using Caliburn.Micro;
using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace HardHorn.ViewModels
{
    public class ParameterViewModel : PropertyChangedBase
    {
        public Archiving.Parameter Parameter { get; private set; }

        public ParameterViewModel(Archiving.Parameter parameter)
        {
            Parameter = parameter;
        }

        public string Representation { get { return Parameter == null ? null : Parameter.ToString(); } }

        public bool HasLength { get { return Parameter != null && Parameter.HasLength; } }
        public bool HasScale { get { return Parameter != null && Parameter.HasScale; } }
        public bool HasPrecision { get { return Parameter != null && Parameter.HasPrecision; } }

        public uint? Length
        {
            get
            {
                return HasLength ? (uint?)Parameter.Length : null;
            }
            set
            {
                if (HasLength != value.HasValue)
                {
                    throw new InvalidOperationException("Parameter does not have a length");
                }
                Parameter.Length = value.Value;
                NotifyOfPropertyChange("Length");
                NotifyOfPropertyChange("Representation");
            }
        }

        public uint? Scale
        {
            get
            {
                return HasScale ? (uint?)Parameter.Scale : null;
            }
            set
            {
                if (HasScale != value.HasValue)
                {
                    throw new InvalidOperationException("Parameter does not have a scale");
                }
                Parameter.Scale = value.Value;
                NotifyOfPropertyChange("Scale");
                NotifyOfPropertyChange("Representation");
            }
        }

        public uint? Precision
        {
            get
            {
                return HasPrecision ? (uint?)Parameter.Precision : null;
            }
            set
            {
                if (HasPrecision != value.HasValue)
                {
                    throw new InvalidOperationException("Parameter does not have a precision");
                }
                Parameter.Precision = value.Value;
                NotifyOfPropertyChange("Precision");
                NotifyOfPropertyChange("Representation");
            }
        }
    }
}
