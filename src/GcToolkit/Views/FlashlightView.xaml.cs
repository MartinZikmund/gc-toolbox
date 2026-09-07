using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class FlashlightViewBase : ViewBase<FlashlightViewModel> { }

public sealed partial class FlashlightView : FlashlightViewBase
{
    public FlashlightView()
    {
        this.InitializeComponent();
    }

    private void OnLitSurfaceTapped(object sender, TappedRoutedEventArgs e) => TurnLightOff();

    private void OnLitSurfaceKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Space or VirtualKey.Enter or VirtualKey.Escape)
        {
            e.Handled = true;
            TurnLightOff();
        }
    }

    /// <summary>Guarded on IsLit so a stray tap can only ever turn the light off, never back on.</summary>
    private void TurnLightOff()
    {
        if (ViewModel is { IsLit: true } viewModel)
        {
            viewModel.ToggleLightCommand.Execute(null);
        }
    }
}
