using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class BookCipherViewBase : ViewBase<BookCipherViewModel> { }

public sealed partial class BookCipherView : BookCipherViewBase
{
    public BookCipherView()
    {
        this.InitializeComponent();
    }
}
