using GcToolkit.Core.Navigation;
using GcToolkit.ViewModels;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class ToolHostViewBase : ViewBase<ToolHostViewModel> { }

public sealed partial class ToolHostView : ToolHostViewBase
{
    public ToolHostView()
    {
        this.InitializeComponent();
    }
}
