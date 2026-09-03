using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class ChecksumViewBase : ViewBase<ChecksumViewModel> { }

public sealed partial class ChecksumView : ChecksumViewBase
{
    public ChecksumView()
    {
        this.InitializeComponent();
    }
}
