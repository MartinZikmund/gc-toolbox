using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class DvorakKeyboardViewBase : ViewBase<DvorakKeyboardViewModel> { }

public sealed partial class DvorakKeyboardView : DvorakKeyboardViewBase
{
    public DvorakKeyboardView()
    {
        this.InitializeComponent();
    }
}
