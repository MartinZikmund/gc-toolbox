using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class GcCodeIdViewBase : ViewBase<GcCodeIdViewModel> { }

public sealed partial class GcCodeIdView : GcCodeIdViewBase
{
    public GcCodeIdView()
    {
        this.InitializeComponent();
    }
}
