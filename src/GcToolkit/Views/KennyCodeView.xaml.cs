using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class KennyCodeViewBase : ViewBase<KennyCodeViewModel> { }

public sealed partial class KennyCodeView : KennyCodeViewBase
{
    public KennyCodeView()
    {
        this.InitializeComponent();
    }
}
