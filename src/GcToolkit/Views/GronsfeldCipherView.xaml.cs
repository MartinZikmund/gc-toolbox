using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class GronsfeldCipherViewBase : ViewBase<GronsfeldCipherViewModel> { }

public sealed partial class GronsfeldCipherView : GronsfeldCipherViewBase
{
    public GronsfeldCipherView()
    {
        this.InitializeComponent();
    }
}
