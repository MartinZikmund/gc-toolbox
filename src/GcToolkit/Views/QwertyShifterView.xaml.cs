using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class QwertyShifterViewBase : ViewBase<QwertyShifterViewModel> { }

public sealed partial class QwertyShifterView : QwertyShifterViewBase
{
    public QwertyShifterView()
    {
        this.InitializeComponent();
    }
}
