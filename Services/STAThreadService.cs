using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

public class STAThreadService
{
    private static STAThreadService _instance;
    private readonly System.Threading.Thread _staThread;
    private readonly ConcurrentQueue<Tuple<Func<object>, TaskCompletionSource<object>>> _taskQueue;
    private readonly AutoResetEvent _hasNewTask;
    private readonly CancellationTokenSource _cts;
    private bool _isRunning;

    private STAThreadService()
    {
        _taskQueue = new ConcurrentQueue<Tuple<Func<object>, TaskCompletionSource<object>>>();
        _hasNewTask = new AutoResetEvent(false);
        _cts = new CancellationTokenSource();
        _isRunning = true;

        _staThread = new System.Threading.Thread(ThreadProc);
        _staThread.SetApartmentState(System.Threading.ApartmentState.STA);
        _staThread.IsBackground = true;
        _staThread.Start();
    }

    public static STAThreadService Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new STAThreadService();
            }
            return _instance;
        }
    }

    private void ThreadProc()
    {
        while (_isRunning)
        {
            if (_hasNewTask.WaitOne(100)) // Wait for tasks with timeout
            {
                while (_taskQueue.TryDequeue(out var task))
                {
                    try
                    {
                        var result = task.Item1();
                        task.Item2.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        task.Item2.TrySetException(ex);
                    }
                }
            }

            if (_cts.Token.IsCancellationRequested)
            {
                _isRunning = false;
            }

            // Process Windows messages
            System.Windows.Forms.Application.DoEvents();
        }
    }

    public Task<T> ExecuteAsync<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<object>();
        _taskQueue.Enqueue(new Tuple<Func<object>, TaskCompletionSource<object>>(() => action(), tcs));
        _hasNewTask.Set();

        return tcs.Task.ContinueWith(t => (T)t.Result);
    }

    public void Shutdown()
    {
        _cts.Cancel();
        _staThread.Join(1000); // Give it a second to shut down
    }
}