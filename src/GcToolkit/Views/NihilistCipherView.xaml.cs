using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class NihilistCipherViewBase : ViewBase<NihilistCipherViewModel> { }

public sealed partial class NihilistCipherView : NihilistCipherViewBase
{
    public NihilistCipherView()
    {
        this.InitializeComponent();
    }
}
