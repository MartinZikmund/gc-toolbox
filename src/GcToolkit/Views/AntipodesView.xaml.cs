using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class AntipodesViewBase : ViewBase<AntipodesViewModel> { }

public sealed partial class AntipodesView : AntipodesViewBase
{
    public AntipodesView()
    {
        this.InitializeComponent();
    }
}
