using Uno.UI.Hosting;

var host = UnoPlatformHostBuilder.Create()
    .App(() => new GcToolkit.App())
    .UseWebAssembly()
    .Build();

await host.RunAsync();
