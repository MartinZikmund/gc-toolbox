using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class SubstitutionCipherViewBase : ViewBase<SubstitutionCipherViewModel> { }

public sealed partial class SubstitutionCipherView : SubstitutionCipherViewBase
{
    public SubstitutionCipherView()
    {
        this.InitializeComponent();
    }
}
