using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class BaseConverterViewBase : ViewBase<BaseConverterViewModel> { }

public sealed partial class BaseConverterView : BaseConverterViewBase
{
    public BaseConverterView()
    {
        this.InitializeComponent();
    }
}
