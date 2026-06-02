using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class CaesarCipherViewBase : ViewBase<CaesarCipherViewModel> { }

public sealed partial class CaesarCipherView : CaesarCipherViewBase
{
    public CaesarCipherView()
    {
        this.InitializeComponent();
    }
}
