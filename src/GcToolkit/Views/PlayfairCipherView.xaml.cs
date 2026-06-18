using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class PlayfairCipherViewBase : ViewBase<PlayfairCipherViewModel> { }

public sealed partial class PlayfairCipherView : PlayfairCipherViewBase
{
    public PlayfairCipherView()
    {
        this.InitializeComponent();
    }
}
