using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class CentroidViewBase : ViewBase<CentroidViewModel> { }

public sealed partial class CentroidView : CentroidViewBase
{
    public CentroidView()
    {
        this.InitializeComponent();
    }
}
