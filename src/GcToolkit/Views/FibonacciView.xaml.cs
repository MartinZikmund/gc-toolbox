using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class FibonacciViewBase : ViewBase<FibonacciViewModel> { }

public sealed partial class FibonacciView : FibonacciViewBase
{
    public FibonacciView()
    {
        this.InitializeComponent();
    }
}
