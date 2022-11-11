using System;
using System.Reactive;
using System.Reactive.Linq;
using Microsoft.Reactive.Testing;
using Nethereum.RPC.Reactive.Polling;
using Xunit;

namespace Nethereum.RPC.Reactive.UnitTests.Polling
{

    public static partial class Polling
    {
        public static System.IObservable<Unit> DefaultPoller = Observable
            .Timer(TimeSpan.FromMilliseconds(250))
            .Select(_ => Unit.Default);

        internal static IObservable<T> Poll<T>(this IObservable<T> query, IObservable<Unit> poller) => poller
            .SelectMany(_ => query)
            .Repeat();

        internal static IObservable<T> Poll<T>(this IObservable<T> query) => query.Poll(DefaultPoller);
    }

    public class PollingTests : ReactiveTest
    {
        [Fact]
        public void PollingTest()
        {
            var sched = new TestScheduler();
            var poller = sched.CreateColdObservable(
                OnNext(100, Unit.Default),
                OnNext(200, Unit.Default),
                OnNext(250, Unit.Default));

            var xs = Observable.Range(0, 2);
            var ys = sched.Start(() => xs.Poll(poller));

            ys.Messages.AssertEqual(
                OnNext(100 + Subscribed, 0),
                OnNext(100 + Subscribed, 1),
                OnNext(200 + Subscribed, 0),
                OnNext(200 + Subscribed, 1),
                OnNext(250 + Subscribed, 0),
                OnNext(250 + Subscribed, 1));
        }
    }
}