using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class OneTimePadViewBase : ViewBase<OneTimePadViewModel> { }

public sealed partial class OneTimePadView : OneTimePadViewBase
{
    public OneTimePadView()
    {
        this.InitializeComponent();
    }
}
