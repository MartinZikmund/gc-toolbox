using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class TextReverseViewBase : ViewBase<TextReverseViewModel> { }

public sealed partial class TextReverseView : TextReverseViewBase
{
    public TextReverseView()
    {
        this.InitializeComponent();
    }
}
