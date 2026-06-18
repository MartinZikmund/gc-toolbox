using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class Base32ViewBase : ViewBase<Base32ViewModel> { }

public sealed partial class Base32View : Base32ViewBase
{
    public Base32View()
    {
        this.InitializeComponent();
    }
}
