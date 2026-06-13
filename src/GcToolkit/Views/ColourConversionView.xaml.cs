using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class ColourConversionViewBase : ViewBase<ColourConversionViewModel> { }

public sealed partial class ColourConversionView : ColourConversionViewBase
{
    public ColourConversionView()
    {
        this.InitializeComponent();
    }
}
