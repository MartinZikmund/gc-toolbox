using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class BeaufortCipherViewBase : ViewBase<BeaufortCipherViewModel> { }

public sealed partial class BeaufortCipherView : BeaufortCipherViewBase
{
    public BeaufortCipherView()
    {
        this.InitializeComponent();
    }
}
