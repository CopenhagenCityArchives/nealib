using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HardHorn.ViewModels
{
    public class RecentLocationViewModel : PropertyChangedBase
    {
        public string Location { get; private set; }
        Action<string> LoadLocationMethod;

        public RecentLocationViewModel(string location, Action<string> loadMethod)
        {
            Location = location;
            LoadLocationMethod = loadMethod;
        }

        public void Load()
        {
            LoadLocationMethod(Location);
        }
    }
}
