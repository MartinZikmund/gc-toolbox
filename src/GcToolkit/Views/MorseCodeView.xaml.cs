using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class MorseCodeViewBase : ViewBase<MorseCodeViewModel> { }

public sealed partial class MorseCodeView : MorseCodeViewBase
{
    public MorseCodeView()
    {
        this.InitializeComponent();
    }
}
