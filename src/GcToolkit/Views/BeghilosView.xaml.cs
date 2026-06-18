using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class BeghilosViewBase : ViewBase<BeghilosViewModel> { }

public sealed partial class BeghilosView : BeghilosViewBase
{
    public BeghilosView()
    {
        this.InitializeComponent();
    }
}
