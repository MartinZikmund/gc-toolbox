using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class Ascii85ViewBase : ViewBase<Ascii85ViewModel> { }

public sealed partial class Ascii85View : Ascii85ViewBase
{
    public Ascii85View()
    {
        this.InitializeComponent();
    }
}
