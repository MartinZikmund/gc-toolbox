using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class ColumnarTranspositionViewBase : ViewBase<ColumnarTranspositionViewModel> { }

public sealed partial class ColumnarTranspositionView : ColumnarTranspositionViewBase
{
    public ColumnarTranspositionView()
    {
        this.InitializeComponent();
    }
}
