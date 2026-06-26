using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class AffineCipherViewBase : ViewBase<AffineCipherViewModel> { }

public sealed partial class AffineCipherView : AffineCipherViewBase
{
    public AffineCipherView()
    {
        this.InitializeComponent();
    }
}
