using System;
using System.Threading.Tasks;

namespace LiveSplit.WsServer.Infrastructure;

/// <summary>
///     Runs work synchronously on the calling thread. Used when there is no
///     form to marshal to (for example in tests).
/// </summary>
public sealed class InlineUiDispatcher : IUiDispatcher
{
    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        try
        {
            return Task.FromResult(func());
        }
        catch (Exception e)
        {
            var completion = new TaskCompletionSource<T>();
            completion.SetException(e);
            return completion.Task;
        }
    }

    public void Post(Action action)
    {
        action();
    }
}
