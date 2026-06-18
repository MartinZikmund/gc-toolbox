using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class ScrabbleScorerViewBase : ViewBase<ScrabbleScorerViewModel> { }

public sealed partial class ScrabbleScorerView : ScrabbleScorerViewBase
{
    public ScrabbleScorerView()
    {
        this.InitializeComponent();
    }
}
