using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class CoordinateConversionViewBase : ViewBase<CoordinateConversionViewModel> { }

public sealed partial class CoordinateConversionView : CoordinateConversionViewBase
{
    public CoordinateConversionView()
    {
        this.InitializeComponent();
    }
}
