using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class AtbashCipherViewBase : ViewBase<AtbashCipherViewModel> { }

public sealed partial class AtbashCipherView : AtbashCipherViewBase
{
    public AtbashCipherView()
    {
        this.InitializeComponent();
    }
}
