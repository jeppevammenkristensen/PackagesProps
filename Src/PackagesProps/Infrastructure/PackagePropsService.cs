using System;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using FileBasedApp.Toolkit.SimpleExec;
using TruePath;

namespace PackagesProps.Infrastructure;

public class PackagePropsService
{
    private readonly IMessenger _messenger;

    public PackagePropsService(IMessenger messenger)
    {
        _messenger = messenger;
    }
    
    
    public async Task CreateProps(AbsolutePath directory, CancellationToken token = default)
    {
        if (!directory.DirectoryExists())
        {
            throw new InvalidOperationException($"The directory {directory} does not exist.");
        }

        _messenger.SetStatusMessage($"Creating Directory.Packages.props in {directory}");
        
        await SimpleExecRunner.Init("dotnet")
            .AddArgumentPair("new", "packagesprops")
            .WithWorkingDirectory(directory)
            .ReadAsync(token:token);
        
        _messenger.SetStatusMessage("Created Directory.Packages.props");
    }
}