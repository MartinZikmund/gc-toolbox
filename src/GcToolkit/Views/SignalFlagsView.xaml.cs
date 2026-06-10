using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class SignalFlagsViewBase : ViewBase<SignalFlagsViewModel> { }

public sealed partial class SignalFlagsView : SignalFlagsViewBase
{
    public SignalFlagsView()
    {
        this.InitializeComponent();
    }
}
