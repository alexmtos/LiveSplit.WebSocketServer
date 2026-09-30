using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LiveSplit.WsServer.Infrastructure;

/// <summary>
///     Marshals work to LiveSplit's UI thread. Must be created on that thread (components are).
/// </summary>
/// <remarks>
///     Control.InvokeRequired is false whenever the form has no handle (before it is shown or while
///     it is closing), which would run the work on the calling thread. Instead, this remembers the UI
///     thread and its SynchronizationContext, which work independently of the form's handle.
/// </remarks>
public sealed class FormUiDispatcher : IUiDispatcher
{
    private readonly Control control;
    private readonly Thread uiThread;
    private readonly SynchronizationContext context;

    public FormUiDispatcher(Control control)
    {
        this.control = control ?? throw new ArgumentNullException(nameof(control));
        uiThread = Thread.CurrentThread;
        // A plain SynchronizationContext would run the work on the thread pool.
        context = SynchronizationContext.Current as WindowsFormsSynchronizationContext;
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            if (context != null)
            {
                if (Thread.CurrentThread == uiThread)
                {
                    Run(func, completion);
                }
                else
                {
                    context.Post(_ => Run(func, completion), null);
                }
            }
            else if (control.IsHandleCreated && !control.IsDisposed)
            {
                // Created off the UI thread: fall back to the form, which needs a handle.
                if (control.InvokeRequired)
                {
                    control.BeginInvoke(new Action(() => Run(func, completion)));
                }
                else
                {
                    Run(func, completion);
                }
            }
            else
            {
                completion.SetException(new LiveSplitUnavailableException());
            }
        }
        catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException)
        {
            // The UI thread is shutting down.
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
///     Thrown when LiveSplit's UI thread is gone (for example while LiveSplit is closing).
/// </summary>
public sealed class LiveSplitUnavailableException : Exception
{
    public LiveSplitUnavailableException()
        : base("LiveSplit is not available.") { }
}
