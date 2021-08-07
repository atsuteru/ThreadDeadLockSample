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
            scheduler.Dispatcher.ShutdownStarted += (s, e) =>
            {
            };
            scheduler.Dispatcher.ShutdownFinished += (s, e) =>
            {
            };
            return scheduler;
        }

        /// <summary>
        /// �X���b�h�f�b�h���b�N���N���Ȃ��ꍇ
        /// </summary>
        [Fact]
        public void Test1()
        {
            TestBody();
        }

        /// <summary>
        /// �X���b�h�f�b�h���b�N���N����ꍇ
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
                //(�C���O)
                //await taskSyncLock.WaitAsync();
                //(�C����)
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

            // ��ɔ񓯊��W�v�����������Ă���
            ProcessStart();

            // ��̔񓯊��W�v�������I���O�ɓ����W�v�������Ă΂��
            ProcessRun();

            // �S�Ă̊�����҂�
            WaitAllTasks();
        }
    }
}
