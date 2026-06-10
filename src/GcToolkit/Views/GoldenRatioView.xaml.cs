using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class GoldenRatioViewBase : ViewBase<GoldenRatioViewModel> { }

public sealed partial class GoldenRatioView : GoldenRatioViewBase
{
    public GoldenRatioView()
    {
        this.InitializeComponent();
    }
}
