using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class RomanNumeralsViewBase : ViewBase<RomanNumeralsViewModel> { }

public sealed partial class RomanNumeralsView : RomanNumeralsViewBase
{
    public RomanNumeralsView()
    {
        this.InitializeComponent();
    }
}
