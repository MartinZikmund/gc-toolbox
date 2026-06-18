using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class FrequencyAnalysisViewBase : ViewBase<FrequencyAnalysisViewModel> { }

public sealed partial class FrequencyAnalysisView : FrequencyAnalysisViewBase
{
    public FrequencyAnalysisView()
    {
        this.InitializeComponent();
    }
}
