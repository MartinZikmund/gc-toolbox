using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class GrayCodeViewBase : ViewBase<GrayCodeViewModel> { }

public sealed partial class GrayCodeView : GrayCodeViewBase
{
    public GrayCodeView()
    {
        this.InitializeComponent();
    }
}
