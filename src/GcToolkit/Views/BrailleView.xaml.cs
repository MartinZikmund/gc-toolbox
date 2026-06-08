using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.UI.Xaml;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class BrailleViewBase : ViewBase<BrailleViewModel> { }

public sealed partial class BrailleView : BrailleViewBase
{
    public BrailleView()
    {
        this.InitializeComponent();
    }

    // Each chart cell carries its BraillePaletteEntry in Tag (ItemsRepeater doesn't set DataContext);
    // forward the click to the VM, which "types" the right character for the current direction.
    private void OnAlphabetItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: BraillePaletteEntry entry })
        {
            ViewModel.InsertCommand.Execute(entry);
        }
    }
}
