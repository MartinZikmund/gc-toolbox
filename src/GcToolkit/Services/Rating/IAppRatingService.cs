namespace GcToolkit.Services.Rating;

public interface IAppRatingService
{
    Task TryPromptForRatingAsync();
}
