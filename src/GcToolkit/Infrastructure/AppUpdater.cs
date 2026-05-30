using GcToolkit.Core.Infrastructure;
using GcToolkit.Services.Settings;

namespace GcToolkit.Infrastructure;

public sealed class AppUpdater : IAppUpdater
{
    private readonly IAppPreferences _preferences;
    private readonly ILogger<AppUpdater> _logger;

    public AppUpdater(IAppPreferences preferences, ILogger<AppUpdater> logger)
    {
        _preferences = preferences;
        _logger = logger;
    }

    public async Task EnsureAppUpToDateAsync()
    {
        if (_preferences.LaunchCount == 0)
        {
            // Fresh install — set to current version
            _preferences.DataVersion = ApplicationReleaseInfo.DataVersion;
            return;
        }

        var retryCount = 0;
        while (_preferences.DataVersion < ApplicationReleaseInfo.DataVersion)
        {
            var versionBefore = _preferences.DataVersion;

            switch (_preferences.DataVersion)
            {
                case 0:
                    await MigrateFromVersion0ToVersion1Async();
                    break;
                    // Add future migration cases here:
                    // case 1:
                    //     await MigrateFromVersion1ToVersion2Async();
                    //     break;
            }

            if (_preferences.DataVersion == versionBefore)
            {
                retryCount++;
                if (retryCount >= 3)
                {
                    _logger.LogError("Data migration stuck at version {Version}. Forcing to current.", versionBefore);
                    _preferences.DataVersion = ApplicationReleaseInfo.DataVersion;
                    break;
                }
            }
            else
            {
                retryCount = 0;
            }
        }
    }

    private Task MigrateFromVersion0ToVersion1Async()
    {
        // Add migration logic here
        _preferences.DataVersion = 1;
        return Task.CompletedTask;
    }
}
