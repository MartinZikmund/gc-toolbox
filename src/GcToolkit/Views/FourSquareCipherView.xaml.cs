using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class FourSquareCipherViewBase : ViewBase<FourSquareCipherViewModel> { }

public sealed partial class FourSquareCipherView : FourSquareCipherViewBase
{
    public FourSquareCipherView()
    {
        this.InitializeComponent();
    }
}
