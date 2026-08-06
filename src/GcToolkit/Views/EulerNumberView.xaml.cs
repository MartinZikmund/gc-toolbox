using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class EulerNumberViewBase : ViewBase<EulerNumberViewModel> { }

public sealed partial class EulerNumberView : EulerNumberViewBase
{
    public EulerNumberView()
    {
        this.InitializeComponent();
    }
}
