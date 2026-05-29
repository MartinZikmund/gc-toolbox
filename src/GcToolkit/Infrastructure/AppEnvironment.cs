namespace GcToolkit.Infrastructure;

public static class AppEnvironment
{
#if APP_CHANNEL_DEV
    public const bool IsDevChannel = true;
    public const string ChannelLabel = "DEV";
#else
    public const bool IsDevChannel = false;
    public const string ChannelLabel = "";
#endif
}
