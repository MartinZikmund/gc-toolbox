using System.Diagnostics.CodeAnalysis;

namespace GcToolkit.Core.Infrastructure;

public static class IoC
{
    private static IServiceProvider? _serviceProvider;

    public static void SetProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public static T? GetService<T>() where T : class
    {
        EnsureServiceProvider();
        return (T?)_serviceProvider.GetService(typeof(T));
    }

    public static T GetRequiredService<T>() where T : class
    {
        EnsureServiceProvider();
        return (T?)_serviceProvider.GetService(typeof(T))
            ?? throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
    }

    [MemberNotNull(nameof(_serviceProvider))]
    private static void EnsureServiceProvider()
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("Service provider was not yet initialized. Call IoC.SetProvider() first.");
        }
    }
}
