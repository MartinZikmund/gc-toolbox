using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.UI.Xaml;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class TapCodeViewBase : ViewBase<TapCodeViewModel> { }

public sealed partial class TapCodeView : TapCodeViewBase
{
    public TapCodeView()
    {
        this.InitializeComponent();
    }

    // Each chart cell carries its TapCodeCell in Tag (ItemsRepeater doesn't set DataContext);
    // forward the click to the VM, which "types" the cell's letter into the Letters field.
    private void OnGridCellClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TapCodeCell cell })
        {
            ViewModel.InsertCommand.Execute(cell);
        }
    }
}
