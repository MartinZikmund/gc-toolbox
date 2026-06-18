using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class AdfgvxCipherViewBase : ViewBase<AdfgvxCipherViewModel> { }

public sealed partial class AdfgvxCipherView : AdfgvxCipherViewBase
{
    public AdfgvxCipherView()
    {
        this.InitializeComponent();
    }
}
