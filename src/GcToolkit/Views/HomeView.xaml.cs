using GcToolkit.Core.Navigation;
using GcToolkit.ViewModels;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Home)]
public partial class HomeViewBase : ViewBase<HomeViewModel> { }

public sealed partial class HomeView : HomeViewBase
{
    public HomeView()
    {
        this.InitializeComponent();
    }
}
