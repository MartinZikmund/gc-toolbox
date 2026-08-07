using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class VigenereCipherViewBase : ViewBase<VigenereCipherViewModel> { }

public sealed partial class VigenereCipherView : VigenereCipherViewBase
{
    public VigenereCipherView()
    {
        this.InitializeComponent();
    }
}
