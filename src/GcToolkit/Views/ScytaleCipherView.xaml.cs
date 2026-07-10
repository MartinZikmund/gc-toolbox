using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class ScytaleCipherViewBase : ViewBase<ScytaleCipherViewModel> { }

public sealed partial class ScytaleCipherView : ScytaleCipherViewBase
{
    public ScytaleCipherView()
    {
        this.InitializeComponent();
    }
}
