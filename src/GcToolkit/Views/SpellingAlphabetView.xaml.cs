using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class SpellingAlphabetViewBase : ViewBase<SpellingAlphabetViewModel> { }

public sealed partial class SpellingAlphabetView : SpellingAlphabetViewBase
{
    public SpellingAlphabetView()
    {
        this.InitializeComponent();
    }
}
