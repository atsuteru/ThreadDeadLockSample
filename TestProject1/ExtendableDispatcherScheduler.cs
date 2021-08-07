using System;
using System.Reactive.Concurrency;
using System.Windows.Threading;

namespace TestProject1
{
    public class ExtendableDispatcherScheduler : DispatcherScheduler
    {
        public ExtendableDispatcherScheduler(Dispatcher dispatcher) : base(dispatcher)
        {
        }

        public override IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            return base.Schedule(state, dueTime, action);
        }

        public override IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
        {
            return base.Schedule(state, action);
        }

        public override IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
        {
            return base.Schedule(state, dueTime, action);
        }
    }
}
