using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.UI.Xaml;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class SegmentDisplayViewBase : ViewBase<SegmentDisplayViewModel> { }

public sealed partial class SegmentDisplayView : SegmentDisplayViewBase
{
    public SegmentDisplayView()
    {
        this.InitializeComponent();
    }

    // Each chart cell carries its SegmentGlyph in Tag (ItemsRepeater doesn't set DataContext);
    // forward the click to the VM, which "types" the right character/code for the current direction.
    private void OnChartItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SegmentGlyph glyph })
        {
            ViewModel.InsertGlyphCommand.Execute(glyph);
        }
    }
}
