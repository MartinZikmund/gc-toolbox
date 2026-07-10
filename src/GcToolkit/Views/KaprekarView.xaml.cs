using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class KaprekarViewBase : ViewBase<KaprekarViewModel> { }

public sealed partial class KaprekarView : KaprekarViewBase
{
    public KaprekarView()
    {
        this.InitializeComponent();
    }
}
