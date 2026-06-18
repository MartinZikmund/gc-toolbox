using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Tool)]
public partial class T9PhoneKeypadViewBase : ViewBase<T9PhoneKeypadViewModel> { }

public sealed partial class T9PhoneKeypadView : T9PhoneKeypadViewBase
{
    public T9PhoneKeypadView()
    {
        this.InitializeComponent();
    }

    // The on-screen keypad lives inside a DataTemplate, where x:Bind can't reach the page VM — so
    // each key Button carries its PhoneKey as Tag and routes the tap through the VM command here.
    private void OnKeyClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PhoneKey key } && ViewModel is { } vm)
        {
            vm.PressKeyCommand.Execute(key.Digit.ToString());
        }
    }
}
