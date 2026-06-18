using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class KeyboardCipherViewBase : ViewBase<KeyboardCipherViewModel> { }

public sealed partial class KeyboardCipherView : KeyboardCipherViewBase
{
    public KeyboardCipherView()
    {
        this.InitializeComponent();
    }
}
