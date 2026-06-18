using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class TrifidCipherViewBase : ViewBase<TrifidCipherViewModel> { }

public sealed partial class TrifidCipherView : TrifidCipherViewBase
{
    public TrifidCipherView()
    {
        this.InitializeComponent();
    }
}
