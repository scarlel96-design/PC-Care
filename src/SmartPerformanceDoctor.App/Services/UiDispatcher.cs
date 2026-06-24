using Microsoft.UI.Dispatching;

namespace SmartPerformanceDoctor.App.Services;

public static class UiDispatcher
{
    public static DispatcherQueue? Queue { get; set; }

    public static bool HasThreadAccess => Queue?.HasThreadAccess == true;

    public static void Run(Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        if (action is null)
        {
            return;
        }

        var queue = Queue;
        if (queue is not null && !queue.HasThreadAccess)
        {
            queue.TryEnqueue(priority, () => action());
            return;
        }

        action();
    }

    public static Task RunAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Run(() => _ = RunCoreAsync(action, tcs), DispatcherQueuePriority.High);
        return tcs.Task;
    }

    public static Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Run(() => _ = RunCoreAsync(action, tcs), DispatcherQueuePriority.High);
        return tcs.Task;
    }

    private static async Task RunCoreAsync(Func<Task> action, TaskCompletionSource tcs)
    {
        try
        {
            await action();
            tcs.TrySetResult();
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    }

    private static async Task RunCoreAsync<T>(Func<Task<T>> action, TaskCompletionSource<T> tcs)
    {
        try
        {
            var result = await action();
            tcs.TrySetResult(result);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    }
}