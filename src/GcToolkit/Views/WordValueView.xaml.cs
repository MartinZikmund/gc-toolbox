using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class WordValueViewBase : ViewBase<WordValueViewModel> { }

public sealed partial class WordValueView : WordValueViewBase
{
    public WordValueView()
    {
        this.InitializeComponent();
    }
}
