using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class RailFenceCipherViewBase : ViewBase<RailFenceCipherViewModel> { }

public sealed partial class RailFenceCipherView : RailFenceCipherViewBase
{
    public RailFenceCipherView()
    {
        this.InitializeComponent();
    }
}
