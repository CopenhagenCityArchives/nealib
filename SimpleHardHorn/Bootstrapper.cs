using System.Windows;
using Caliburn.Micro;
using System.Windows.Markup;
using System.Globalization;
using System.Linq;

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
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
            DisplayRootViewFor<ViewModels.SimpleViewModel>();
        }
    }
}
