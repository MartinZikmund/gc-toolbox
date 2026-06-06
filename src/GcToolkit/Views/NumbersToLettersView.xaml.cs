using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class NumbersToLettersViewBase : ViewBase<NumbersToLettersViewModel> { }

public sealed partial class NumbersToLettersView : NumbersToLettersViewBase
{
    public NumbersToLettersView()
    {
        this.InitializeComponent();
    }
}
