using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class PrimeNumbersViewBase : ViewBase<PrimeNumbersViewModel> { }

public sealed partial class PrimeNumbersView : PrimeNumbersViewBase
{
    public PrimeNumbersView()
    {
        this.InitializeComponent();
    }
}
