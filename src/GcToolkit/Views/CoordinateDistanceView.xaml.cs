using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class CoordinateDistanceViewBase : ViewBase<CoordinateDistanceViewModel> { }

public sealed partial class CoordinateDistanceView : CoordinateDistanceViewBase
{
    public CoordinateDistanceView()
    {
        this.InitializeComponent();
    }
}
