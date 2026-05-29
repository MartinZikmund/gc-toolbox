using GcToolkit.Core.Navigation;
using GcToolkit.ViewModels;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Main)]
public partial class MainViewBase : ViewBase<MainViewModel> { }

public sealed partial class MainView : MainViewBase
{
    public MainView()
    {
        this.InitializeComponent();
    }
}
