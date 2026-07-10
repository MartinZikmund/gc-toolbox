using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class PiViewBase : ViewBase<PiViewModel> { }

public sealed partial class PiView : PiViewBase
{
    public PiView()
    {
        this.InitializeComponent();
    }
}
