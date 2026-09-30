using System;
using System.Threading.Tasks;

namespace LiveSplit.WsServer.Infrastructure;

/// <summary>
///     Runs work on LiveSplit's UI thread, which is the only thread allowed
///     to touch the timer, the run and the layout.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    ///     Runs <paramref name="func"/> on the UI thread and completes with its result.
    /// </summary>
    Task<T> InvokeAsync<T>(Func<T> func);

    /// <summary>
    ///     Queues <paramref name="action"/> on the UI thread without waiting for it.
    /// </summary>
    void Post(Action action);
}
