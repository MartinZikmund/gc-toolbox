using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class KamasutraCipherViewBase : ViewBase<KamasutraCipherViewModel> { }

public sealed partial class KamasutraCipherView : KamasutraCipherViewBase
{
    public KamasutraCipherView()
    {
        this.InitializeComponent();
    }
}
