using ScHost.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("schost");
    config.SetApplicationVersion("1.0.0");

    config.AddCommand<InitCommand>("init")
        .WithDescription("Initialize schost.json for a project")
        .WithExample("init")
        .WithExample("init", "MyApp/MyApp.csproj");

    config.AddCommand<RunCommand>("run")
        .WithDescription("Build and launch the app in a configured terminal")
        .WithExample("run")
        .WithExample("run", "--no-build")
        .WithExample("run", "--inline");

    config.AddCommand<PackCommand>("pack")
        .WithDescription("Package the app for distribution")
        .WithExample("pack")
        .WithExample("pack", "--installer")
        .WithExample("pack", "-r", "win-x64");

    config.AddCommand<InstallCommand>("install")
        .WithDescription("Register/unregister terminal profile")
        .WithExample("install", "--exe", "path/to/app.exe")
        .WithExample("install", "--uninstall");
});

return app.Run(args);
