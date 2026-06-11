using System;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class PhoneKeypadViewBase : ViewBase<PhoneKeypadViewModel> { }

/// <summary>
/// Interactive phone keypad. The View owns the one piece of UI timing the multitap experience needs:
/// a <see cref="DispatcherTimer"/> restarted on every key press and fired on timeout to commit the
/// pending letter. A fast re-tap of the same key therefore cycles its letters; a slow tap lands on a
/// fresh one. All letter logic lives in the ViewModel/composer, so this code-behind only forwards
/// presses and manages the timer.
/// </summary>
public sealed partial class PhoneKeypadView : PhoneKeypadViewBase
{
    private readonly DispatcherTimer _multitapTimer = new();

    public PhoneKeypadView()
    {
        this.InitializeComponent();
        _multitapTimer.Tick += OnMultitapTimeout;
        Unloaded += (_, _) => _multitapTimer.Stop();
    }

    private void OnKeyClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not FrameworkElement { Tag: string tag } || tag.Length == 0)
        {
            return;
        }

        ViewModel.PressKey(tag[0]);
        RestartMultitapTimer();
    }

    private void RestartMultitapTimer()
    {
        _multitapTimer.Stop();
        _multitapTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, ViewModel?.MultitapTimeoutMs ?? 1000));
        _multitapTimer.Start();
    }

    private void OnMultitapTimeout(object? sender, object e)
    {
        _multitapTimer.Stop();
        ViewModel?.CommitPending();
    }

    private void OnBackspaceClick(object sender, RoutedEventArgs e)
    {
        _multitapTimer.Stop();
        ViewModel?.Backspace();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        _multitapTimer.Stop();
        ViewModel?.ClearComposed();
    }
}
