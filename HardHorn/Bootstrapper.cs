using System.Windows;
using Caliburn.Micro;

namespace HardHorn
{
    public class Bootstrapper : BootstrapperBase
    {
        public Bootstrapper()
        {
            Initialize();
        }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            DisplayRootViewFor<ViewModels.MainViewModel>();
        }
    }
}
