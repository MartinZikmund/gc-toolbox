using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class NumbersToWordsViewBase : ViewBase<NumbersToWordsViewModel> { }

public sealed partial class NumbersToWordsView : NumbersToWordsViewBase
{
    public NumbersToWordsView()
    {
        this.InitializeComponent();
    }
}
