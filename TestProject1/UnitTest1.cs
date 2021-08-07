using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;
using Xunit.Abstractions;

namespace TestProject1
{
    public class UnitTest1
    {
        public ITestOutputHelper TestOutputHelper { get; }

        public UnitTest1(ITestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper;
        }

        public static ExtendableDispatcherScheduler RunExtendableSchedulableThread(string threadName)
        {
            ExtendableDispatcherScheduler scheduler = null;
            Task.Run(() =>
            {
                try
                {
                    var currentDispatcher = Dispatcher.CurrentDispatcher;
                    currentDispatcher.Thread.Name = threadName;
                    scheduler = new ExtendableDispatcherScheduler(currentDispatcher);
                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                }
            });
            while (scheduler == null)
            {
                Thread.Sleep(10);
            }
            return scheduler;
        }

        /// <summary>
        /// スレッドデッドロックが起きない場合
        /// </summary>
        [Fact]
        public void Test1()
        {
            TestBody();
        }

        /// <summary>
        /// スレッドデッドロックが起きる場合
        /// </summary>
        [Fact]
        public void Test2()
        {
            DispatcherScheduler managerScheduler = RunExtendableSchedulableThread("Manager");

            var isEnd = false;
            managerScheduler.Schedule(() =>
            {
                TestBody();

                isEnd = true;
            });

            while(!isEnd)
            {
                Thread.Sleep(20);
            }
        }

        private void TestBody()
        {
            var taskSyncLock = new SemaphoreSlim(1, 1);
            var runningTasks = new ConcurrentDictionary<int, Task>();

            Task ProcessStart()
            {
                return ProcessAsync();
            }

            void ProcessRun()
            {
                ProcessAsync().Wait();
            }

            async Task ProcessAsync()
            {
                TestOutputHelper.WriteLine($"ProcessAsync called. waiting start...");
                //(修正前)
                //await taskSyncLock.WaitAsync();
                //(修正後)
                await taskSyncLock.WaitAsync().ConfigureAwait(false);
                TestOutputHelper.WriteLine($"ProcessAsync start");
                try
                {
                    var task = Task
                        .Run(() => Thread.Sleep(3000))
                        .ContinueWith(t =>
                        {
                            runningTasks.TryRemove(t.Id, out var removed);
                        });
                    runningTasks.TryAdd(task.Id, task);
                    await task.ConfigureAwait(false);
                }
                finally
                {
                    TestOutputHelper.WriteLine($"ProcessAsync end");
                    taskSyncLock.Release();
                }
            }

            void WaitAllTasks()
            {
                taskSyncLock.Wait();
                TestOutputHelper.WriteLine($"WaitAllTasks start");
                try
                {
                    Task.WaitAll(runningTasks.Values.ToArray());
                }
                finally
                {
                    TestOutputHelper.WriteLine($"WaitAllTasks end");
                    taskSyncLock.Release();
                }
            }

            // 先に非同期処理が走っている状態を作る
            ProcessStart();
            // 先の非同期処理が終わる前に同期処理を呼ぶ
            ProcessRun();
            // 全ての完了を待つ
            WaitAllTasks();
        }

        /// <summary>
        /// ConfigureAwait(false)を使う場合の実行スレッドの違い・・・待った後は別スレッドで継続される。
        /// </summary>
        /// <remarks>
        /// Case: await taskSyncLock.WaitAsync().ConfigureAwait(false);
        ///   [9]Schedule 1 start.
        ///   [9]ProcessAsync called. waiting start...
        ///   [9]ProcessAsync start
        ///   [9]await start
        ///   [9]Schedule 1 end.
        ///   [9]Schedule 2 start.
        ///   [9]ProcessAsync called. waiting start...
        ///   [31]await end
        ///   [31]ProcessAsync end
        ///   [31]ProcessAsync start ★ワーカースレッドで実行されている
        ///   [31]await start
        ///   [10]await end
        ///   [10]ProcessAsync end
        ///   [9]WaitAllTasks start
        ///   [9]WaitAllTasks end
        ///   [9]Schedule 2 end.
        ///   [9]Schedule 3 start.
        ///   [9]Schedule 3 end.
        /// Case: taskSyncLock.Wait()・・・待った後は元のスレッドで継続される。
        ///   [9]Schedule 1 start.
        ///   [9]ProcessAsync called. waiting start...
        ///   [9]ProcessAsync start
        ///   [9]await start
        ///   [9]Schedule 1 end.
        ///   [9]Schedule 2 start.
        ///   [9]ProcessAsync called. waiting start...
        ///   [10]await end
        ///   [10]ProcessAsync end
        ///   [9]ProcessAsync start ★Managerスレッドで実行されている
        ///   [9]await start
        ///   [31]await end
        ///   [31]ProcessAsync end
        ///   [9]WaitAllTasks start
        ///   [9]WaitAllTasks end
        ///   [9]Schedule 2 end.
        ///   [9]Schedule 3 start.
        ///   [9]Schedule 3 end.
        /// </remarks>
        [Fact]
        public void Test3()
        {
            var taskSyncLock = new SemaphoreSlim(1, 1);
            var runningTasks = new ConcurrentDictionary<int, Task>();

            Task ProcessStart()
            {
                return ProcessAsync();
            }

            void ProcessRun()
            {
                ProcessAsync().Wait();
            }

            async Task ProcessAsync()
            {
                TestOutputHelper.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]ProcessAsync called. waiting start...");
                await taskSyncLock.WaitAsync().ConfigureAwait(false);
                //taskSyncLock.Wait();
                TestOutputHelper.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]ProcessAsync start");
                try
                {
                    var task = Task
                        .Run(() => Thread.Sleep(3000))
                        .ContinueWith(t =>
                        {
                            runningTasks.TryRemove(t.Id, out var removed);
                        });
                    runningTasks.TryAdd(task.Id, task);
                    TestOutputHelper.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]await start");
                    await task.ConfigureAwait(false);
                    TestOutputHelper.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]await end");
                }
                finally
                {
                    TestOutputHelper.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]ProcessAsync end");
                    taskSyncLock.Release();
                }
            }

            void WaitAllTasks()
            {
                taskSyncLock.Wait();
                TestOutputHelper.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]WaitAllTasks start");
                try
                {
                    Task.WaitAll(runningTasks.Values.ToArray());
                }
                finally
                {
                    TestOutputHelper.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]WaitAllTasks end");
                    taskSyncLock.Release();
                }
            }

            var isEnd = false;
            DispatcherScheduler managerScheduler = RunExtendableSchedulableThread("Manager");

            managerScheduler.Schedule(() =>
            {
                TestOutputHelper.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]Schedule 1 start.");
                // 先に非同期処理が走る
                ProcessStart();
                TestOutputHelper.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]Schedule 1 end.");
            });

            managerScheduler.Schedule(() =>
            {
                TestOutputHelper.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]Schedule 2 start.");
                // 先の非同期処理が終わる前に同期処理が走る
                ProcessRun();
                // 全ての完了を待つ
                WaitAllTasks();

                isEnd = true;
                TestOutputHelper.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]Schedule 2 end.");
            });

            managerScheduler.Schedule(() =>
            {
                TestOutputHelper.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]Schedule 3 start.");
                TestOutputHelper.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}]Schedule 3 end.");
            });

            while (!isEnd)
            {
                Thread.Sleep(20);
            }
        }
    }
}
