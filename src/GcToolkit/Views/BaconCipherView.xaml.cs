using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class BaconCipherViewBase : ViewBase<BaconCipherViewModel> { }

public sealed partial class BaconCipherView : BaconCipherViewBase
{
    public BaconCipherView()
    {
        this.InitializeComponent();
    }
}
