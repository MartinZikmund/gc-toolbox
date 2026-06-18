using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class NumerologyViewBase : ViewBase<NumerologyViewModel> { }

public sealed partial class NumerologyView : NumerologyViewBase
{
    public NumerologyView()
    {
        this.InitializeComponent();
    }
}
