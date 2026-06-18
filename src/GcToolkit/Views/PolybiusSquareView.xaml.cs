using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class PolybiusSquareViewBase : ViewBase<PolybiusSquareViewModel> { }

public sealed partial class PolybiusSquareView : PolybiusSquareViewBase
{
    public PolybiusSquareView()
    {
        this.InitializeComponent();
    }
}
