using System.Collections.Concurrent;

namespace AdsWrapper
{
    /// <summary>
    /// Provides a dedicated background thread for executing actions sequentially.
    /// On Windows, the thread is configured as a single-threaded apartment (STA) thread.
    /// </summary>
    public class DedicatedThreadRunner : IDisposable
    {
        #region  private fields
        /// <summary> The dedicated thread used to process queued operations </summary>
        private readonly Thread thread;
        /// <summary> The queue containing the operations to be executed by the dedicated thread </summary>
        private readonly BlockingCollection<Func<Task>> queue = [];
        /// <summary> The cancellation token source used to stop the message loop </summary>
        private readonly CancellationTokenSource cts = new();
        /// <summary> Indicates whether the object has been disposed </summary>
        private bool disposed;
        #endregion

        #region constructors
        /// <summary>
        /// Initializes a new instance of this classes and starts the dedicated thread.
        /// </summary>
        public DedicatedThreadRunner()
        {
            thread = new Thread(RunMessageLoop)
            {
                IsBackground = true,
                Name = "Dedicated STA Runner",
            };
            if (OperatingSystem.IsWindows())
            {
                thread.SetApartmentState(ApartmentState.STA);
            }
            thread.Start();
        }
        #endregion

        #region public methods and tasks
        /// <summary>
        /// Queues an action for execution on the dedicated thread and returns a task that completes when finished.
        /// </summary>
        /// <param name="action"> The action to execute on the dedicated thread </param>
        /// <returns> A task representing the asynchronous completion of the action. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="action"/> is null. </exception>
        public Task InvokeAsync(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            queue.Add(() =>
            {
                try
                {
                    action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }

                return Task.CompletedTask;
            });

            return tcs.Task;
        }

        /// <summary>
        /// Queues a synchronous action for execution on the dedicated thread
        /// without waiting for it to complete.
        /// </summary>
        /// <param name="work"> The action to queue for execution. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="work"/> is <see langword="null"/>. </exception>
        public void BeginInvoke(Action work)
        {
            ArgumentNullException.ThrowIfNull(work);
            InvokeAsync(() =>
            {
                work();
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Releases all resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region private methods and tasks
        /// <summary>
        /// Releases all resources used by this instance.
        /// </summary>
        /// <param name="disposing"> Disposes this istance if TRUE </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }
            if (disposing)
            {
                cts.Cancel();
                queue.CompleteAdding();
                if (!thread.Join(TimeSpan.FromSeconds(5)))
                {
                    thread.Interrupt();
                }
                cts.Dispose();
                queue.Dispose();
            }
            disposed = true;
        }

        /// <summary>
        /// Queues a synchronous function for execution on the dedicated thread and returns a task that completes with the function's result.
        /// </summary>
        /// <typeparam name="TResult"> The type of the result returned by the function </typeparam>
        /// <param name="func"> The function to execute on the dedicated thread </param>
        /// <returns> A task representing the asynchronous execution of the function. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="func"/> is <see langword="null"/>. </exception>
        private Task<TResult> InvokeAsync<TResult>(Func<TResult> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Wrapper()
            {
                try
                {
                    var result = func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }
            queue.Add(() =>
            {
                Wrapper();
                return Task.CompletedTask;
            });
            return tcs.Task;
        }

        /// <summary>
        /// Runs the message loop that processes queued operations sequentially.
        /// </summary>
        private void RunMessageLoop()
        {
            try
            {
                foreach (var workItem in queue.GetConsumingEnumerable(cts.Token))
                {
                    var task = workItem();
                    task.GetAwaiter().GetResult();
                }
            }
            catch (OperationCanceledException) { }
        }
        #endregion
    }
}
