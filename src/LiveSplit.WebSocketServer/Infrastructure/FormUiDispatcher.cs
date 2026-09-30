using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LiveSplit.WsServer.Infrastructure;

/// <summary>
///     Marshals work to the thread that owns LiveSplit's main form.
/// </summary>
public sealed class FormUiDispatcher : IUiDispatcher
{
    private readonly Control control;

    public FormUiDispatcher(Control control)
    {
        this.control = control ?? throw new ArgumentNullException(nameof(control));
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (control.IsDisposed)
        {
            completion.SetException(new LiveSplitUnavailableException());
            return completion.Task;
        }

        if (!control.InvokeRequired)
        {
            Run(func, completion);
            return completion.Task;
        }

        try
        {
            control.BeginInvoke(new Action(() => Run(func, completion)));
        }
        catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException)
        {
            completion.TrySetException(new LiveSplitUnavailableException());
        }

        return completion.Task;
    }

    public void Post(Action action)
    {
        _ = InvokeAsync(() =>
        {
            action();
            return true;
        });
    }

    private static void Run<T>(Func<T> func, TaskCompletionSource<T> completion)
    {
        try
        {
            completion.TrySetResult(func());
        }
        catch (Exception e)
        {
            completion.TrySetException(e);
        }
    }
}

/// <summary>
///     Thrown when LiveSplit's main form is gone (for example while LiveSplit is closing).
/// </summary>
public sealed class LiveSplitUnavailableException : Exception
{
    public LiveSplitUnavailableException()
        : base("LiveSplit is not available.") { }
}
