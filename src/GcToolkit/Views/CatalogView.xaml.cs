using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Catalog)]
public partial class CatalogViewBase : ViewBase<CatalogViewModel> { }

public sealed partial class CatalogView : CatalogViewBase
{
    public CatalogView()
    {
        this.InitializeComponent();
    }
}
