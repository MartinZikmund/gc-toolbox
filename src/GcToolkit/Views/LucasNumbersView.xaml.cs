using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class LucasNumbersViewBase : ViewBase<LucasNumbersViewModel> { }

public sealed partial class LucasNumbersView : LucasNumbersViewBase
{
    public LucasNumbersView()
    {
        this.InitializeComponent();
    }
}
