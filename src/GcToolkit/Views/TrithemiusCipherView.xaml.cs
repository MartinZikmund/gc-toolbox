using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class TrithemiusCipherViewBase : ViewBase<TrithemiusCipherViewModel> { }

public sealed partial class TrithemiusCipherView : TrithemiusCipherViewBase
{
    public TrithemiusCipherView()
    {
        this.InitializeComponent();
    }
}
