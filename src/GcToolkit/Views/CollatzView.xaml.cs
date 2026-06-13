using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class CollatzViewBase : ViewBase<CollatzViewModel> { }

public sealed partial class CollatzView : CollatzViewBase
{
    public CollatzView()
    {
        this.InitializeComponent();
    }
}
