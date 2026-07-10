using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class AngleConversionViewBase : ViewBase<AngleConversionViewModel> { }

public sealed partial class AngleConversionView : AngleConversionViewBase
{
    public AngleConversionView()
    {
        this.InitializeComponent();
    }
}
