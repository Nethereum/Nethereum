using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.RPC.Reactive.Extensions
{
    public static class SubscriptionEventHandlerExtensions
    {
        public static IObservable<TSubscriptionDataResponse> GetDataObservable<TSubscriptionDataResponse>(
            this RpcStreamingSubscriptionEventResponseHandler<TSubscriptionDataResponse> rpcEventHandler) =>
            Observable
                .Create<TSubscriptionDataResponse>(o =>
                {
                    var evt = Observable.FromEventPattern<StreamingEventArgs<TSubscriptionDataResponse>>(
                        h => rpcEventHandler.SubscriptionDataResponse += h,
                        h => rpcEventHandler.SubscriptionDataResponse -= h);

                    var onSuccess = evt
                        .Where(e => e.EventArgs.Exception == null)
                        .Select(e => e.EventArgs.Response)
                        .Subscribe(o);

                    var onError = evt
                        .Where(e => e.EventArgs.Exception != null)
                        .Select(e => e.EventArgs.Exception)
                        .Subscribe(o.OnError);

                    var onCompleted = rpcEventHandler
                        .GetUnsubscribeObservable()
                        .Subscribe(
                            _ => { },
                            _ => { },
                            o.OnCompleted);

                    return new CompositeDisposable { onSuccess, onError, onCompleted };
                })
                .Publish()
                .RefCount();

        public static IObservable<string>
            GetSubscribeObservable<TSubscriptionDataResponse>(this RpcStreamingSubscriptionEventResponseHandler<TSubscriptionDataResponse> rpcEventHandler) =>
            Observable
                .Create<string>(o =>
                {
                    var evt = Observable.FromEventPattern<StreamingEventArgs<string>>(
                        h => rpcEventHandler.SubscribeResponse += h,
                        h => rpcEventHandler.SubscribeResponse -= h);

                    var onSuccess = evt
                        .Where(e => e.EventArgs.Exception == null)
                        .Select(e => e.EventArgs.Response)
                        .Subscribe(o);

                    var onError = evt
                        .Where(e => e.EventArgs.Exception != null)
                        .Select(e => e.EventArgs.Exception)
                        .Subscribe(o.OnError);

                    return new CompositeDisposable { onSuccess, onError };
                })
                .Take(1)
                .Publish()
                .RefCount();

        public static IObservable<bool>
            GetUnsubscribeObservable<TSubscriptionDataResponse>(this RpcStreamingSubscriptionEventResponseHandler<TSubscriptionDataResponse> rpcEventHandler) =>
            Observable
                .Create<bool>(o =>
                {
                    var evt = Observable.FromEventPattern<StreamingEventArgs<bool>>(
                        h => rpcEventHandler.UnsubscribeResponse += h,
                        h => rpcEventHandler.UnsubscribeResponse -= h);

                    var onSuccess = evt
                        .Where(e => e.EventArgs.Exception == null)
                        .Select(e => e.EventArgs.Response)
                        .Subscribe(o);

                    var onError = evt
                        .Where(e => e.EventArgs.Exception != null)
                        .Select(e => e.EventArgs.Exception)
                        .Subscribe(o.OnError);

                    return new CompositeDisposable { onSuccess, onError };
                })
                .Take(1)
                .Publish()
                .RefCount();
    }
}