using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class CoordinateAveragingViewBase : ViewBase<CoordinateAveragingViewModel> { }

public sealed partial class CoordinateAveragingView : CoordinateAveragingViewBase
{
    public CoordinateAveragingView()
    {
        this.InitializeComponent();
    }
}
