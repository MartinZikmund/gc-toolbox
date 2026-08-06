using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class BrainfuckOokViewBase : ViewBase<BrainfuckOokViewModel> { }

public sealed partial class BrainfuckOokView : BrainfuckOokViewBase
{
    public BrainfuckOokView()
    {
        this.InitializeComponent();
    }
}
