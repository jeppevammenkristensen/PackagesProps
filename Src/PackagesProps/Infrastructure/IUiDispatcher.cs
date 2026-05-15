using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace PackagesProps.Infrastructure;

/// <summary>
/// Abstraction over the UI thread dispatcher so that view models can marshal
/// property updates back to the UI without taking a hard dependency on
/// <see cref="Dispatcher"/>. Tests can substitute a synchronous implementation.
/// </summary>
public interface IUiDispatcher
{
    Task InvokeAsync(Action action);
}

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public Task InvokeAsync(Action action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();
}
