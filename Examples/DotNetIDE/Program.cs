if (SharpConsoleUI.PtyShim.RunIfShim(args)) Environment.Exit(127);

var projectPath = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();

using var app = new DotNetIDE.IdeApp(projectPath);
app.Run();
