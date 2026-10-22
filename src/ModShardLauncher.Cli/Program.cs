using ModShardLauncher;
using ModShardLauncher.Cli;
using Serilog;

Console.OutputEncoding = System.Text.Encoding.UTF8;

CliArguments arguments;
try
{
    arguments = CliArguments.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    CliArguments.PrintUsage();
    return 1;
}

if (arguments.ShowHelp)
{
    CliArguments.PrintUsage();
    return 0;
}

if (arguments.ShowVersion)
{
    Console.WriteLine($"{AppInfo.ProductName} {AppInfo.MslVersion} (UndertaleModTool {AppInfo.UtmtLibVersion})");
    return 0;
}

AppPaths.BaseDirectory = arguments.BaseDirectory;
AppPaths.EnsureCreated();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(Path.Combine(AppPaths.LogsDirectory, $"cli_{DateTime.Now:yyyyMMdd_HHmmss}.log"))
    .CreateLogger();

try
{
    return arguments.Command switch
    {
        "patch" => await Commands.PatchAsync(arguments),
        "list" => Commands.List(arguments),
        "compile" => await Commands.CompileAsync(arguments),
        "export" => await Commands.ExportAsync(arguments),
        _ => Unknown(arguments.Command),
    };
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled failure");
    Console.Error.WriteLine(ex.Message);
    return 3;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    CliArguments.PrintUsage();
    return 1;
}
