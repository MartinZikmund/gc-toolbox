using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class SemaphoreViewBase : ViewBase<SemaphoreViewModel> { }

public sealed partial class SemaphoreView : SemaphoreViewBase
{
    public SemaphoreView()
    {
        this.InitializeComponent();
    }
}
