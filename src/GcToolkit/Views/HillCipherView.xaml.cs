using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class HillCipherViewBase : ViewBase<HillCipherViewModel> { }

public sealed partial class HillCipherView : HillCipherViewBase
{
    public HillCipherView()
    {
        this.InitializeComponent();
    }
}
