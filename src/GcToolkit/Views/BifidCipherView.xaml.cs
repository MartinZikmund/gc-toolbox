using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class BifidCipherViewBase : ViewBase<BifidCipherViewModel> { }

public sealed partial class BifidCipherView : BifidCipherViewBase
{
    public BifidCipherView()
    {
        this.InitializeComponent();
    }
}
