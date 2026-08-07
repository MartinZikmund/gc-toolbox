using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class AsciiConverterViewBase : ViewBase<AsciiConverterViewModel> { }

public sealed partial class AsciiConverterView : AsciiConverterViewBase
{
    public AsciiConverterView()
    {
        this.InitializeComponent();
    }
}
