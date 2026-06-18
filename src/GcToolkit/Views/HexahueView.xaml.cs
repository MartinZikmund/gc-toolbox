using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class HexahueViewBase : ViewBase<HexahueViewModel> { }

public sealed partial class HexahueView : HexahueViewBase
{
    public HexahueView()
    {
        this.InitializeComponent();
    }
}
