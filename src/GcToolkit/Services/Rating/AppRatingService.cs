using GcToolkit.Core.Services.Dialogs;
using GcToolkit.Core.Services.Settings;
using GcToolkit.Services.Localization;

namespace GcToolkit.Services.Rating;

public sealed class AppRatingService : IAppRatingService
{
    private const int MinLaunchCountForRating = 5;

    private readonly IAppPreferences _appPreferences;
    private readonly IConfirmationDialogService _confirmationDialogService;
    private readonly ILauncherService _launcherService;

    public AppRatingService(
        IAppPreferences appPreferences,
        IConfirmationDialogService confirmationDialogService,
        ILauncherService launcherService)
    {
        _appPreferences = appPreferences;
        _confirmationDialogService = confirmationDialogService;
        _launcherService = launcherService;
    }

    public async Task TryPromptForRatingAsync()
    {
        if (!_appPreferences.OfferUserRating)
        {
            return;
        }

        if (_appPreferences.LaunchCount < MinLaunchCountForRating)
        {
            return;
        }

        var result = await _confirmationDialogService.ShowAsync(
            Localizer.Instance.GetString("RateAppTitle"),
            Localizer.Instance.GetString("RateAppMessage"));

        if (result == ConfirmationResult.Confirmed)
        {
            _appPreferences.OfferUserRating = false;
            await LaunchStoreReviewAsync();
        }
        else
        {
            // User declined — don't ask again
            _appPreferences.OfferUserRating = false;
        }
    }

    private async Task LaunchStoreReviewAsync()
    {
        var uri = new Uri("ms-windows-store://review/?ProductId=REPLACE_WITH_STORE_ID");
        await _launcherService.LaunchUriAsync(uri);
    }
}
