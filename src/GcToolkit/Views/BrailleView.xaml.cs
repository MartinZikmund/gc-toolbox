using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class BrailleViewBase : ViewBase<BrailleViewModel> { }

public sealed partial class BrailleView : BrailleViewBase
{
    public BrailleView()
    {
        this.InitializeComponent();
    }
}
