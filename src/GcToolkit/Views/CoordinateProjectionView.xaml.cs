using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class CoordinateProjectionViewBase : ViewBase<CoordinateProjectionViewModel> { }

public sealed partial class CoordinateProjectionView : CoordinateProjectionViewBase
{
    public CoordinateProjectionView()
    {
        this.InitializeComponent();
    }
}
