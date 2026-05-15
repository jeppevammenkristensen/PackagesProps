#:package FileBasedApp.Toolkit@0.20.0
#:property PublishAot=false 
using Spectre.Console.Cli;
using TruePath;
using Spectre.Console;
using FileBasedApp.Toolkit;
using FileBasedApp.Toolkit.SimpleExec;
using System.IO.Abstractions;
using TruePath.TestableIO.System.IO;
using FileBasedApp.Toolkit.CommandCli;

var commandApp = new CommandApp<RunCommand>().WithDescription("Enter the description here");
commandApp.Configure(ctx =>
{
    ctx.PropagateExceptions();
});
return await commandApp.RunAsync(args);
public class RunCommand : AsyncCommand<RunCommand.Settings> // For sync only you can use Command (and have Execute instead of ExecuteAsync
{
    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var root = PathUtil.GetExecutionFolder() / ".."; // navigate out of the scripts folder one level up
        var projectPath = root / "Src" / "PackagesProps" / "PackagesProps.csproj";
        if (!projectPath.FileExists())
        {
            throw new InvalidOperationException($"Cannot locate project file at  {projectPath}. Current execution folder is {PathUtil.GetExecutionFolder()}");
        }
        
        var (version, _) = await SimpleExecRunner.Init("dotnet")
            .AddArgumentPair("msbuild", projectPath)
            .AddArgument("-getProperty:Version")
            .AddArgument("-p:Configuration=Release")
            .ReadAsync(token: cancellationToken);
        
        version = version.TrimEnd('\r', '\n');
        
        AnsiConsole.MarkupLineInterpolated($"[green]Version is {version}[/]");
        
        
        var publishPath = root / "PublishOutput";
        try
        {
            publishPath.CreateDirectory();
            
            AnsiConsole.Write(new Rule("Publish"));
            
            await SimpleExecRunner.Init("dotnet")
                .AddArgumentPair("publish",projectPath)
                .AddArgument("--output")
                .AddArgument(publishPath)
                .AddArgument("--configuration")
                .AddArgument("Release")
                .AddArgument("--self-contained")
                .AddArgument("true")
                .AddArgument("--runtime")
                .AddArgument("win-x64") // Change this to your target runtime
                .WithWorkingDirectory(root)
                .RunAsync(token: cancellationToken);
            
            AnsiConsole.Write(new Rule("Pack with Velopack"));
            
            await SimpleExecRunner.Init("vpk")
                .AddArgument("pack")
                .AddArgumentPair("--packId", "PackagesProps")
                .AddArgumentPair("--packVersion", version)
                .AddArgumentPair("--packDir", publishPath)
                .AddArgumentPair("--mainExe", "PackagesProps.exe")
                .WithWorkingDirectory(root)
                .RunAsync(token: cancellationToken);
            
            return 0; // 0 for success
        }
        finally
        {
            publishPath.SafeDeleteDirectory();
        }
    }

    public class Settings : ExtendedCommandSettings
    {
    }
}