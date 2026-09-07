using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class Base64ViewBase : ViewBase<Base64ViewModel> { }

public sealed partial class Base64View : Base64ViewBase
{
    public Base64View()
    {
        this.InitializeComponent();
    }
}
