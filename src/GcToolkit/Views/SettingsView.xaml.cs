using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Settings)]
public partial class SettingsViewBase : ViewBase<SettingsViewModel> { }

public sealed partial class SettingsView : SettingsViewBase
{
    public SettingsView()
    {
        this.InitializeComponent();
    }
}
