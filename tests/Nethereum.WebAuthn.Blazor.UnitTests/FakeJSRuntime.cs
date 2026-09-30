using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Nethereum.WebAuthn.Blazor.UnitTests
{
    internal class FakeJSRuntime : IJSRuntime
    {
        public FakeJSObjectReference Module { get; } = new FakeJSObjectReference();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier != "import")
            {
                throw new InvalidOperationException($"Unexpected top-level JS interop call: {identifier}");
            }

            return new ValueTask<TValue>((TValue)(object)Module);
        }
    }

    internal class FakeJSObjectReference : IJSObjectReference
    {
        public Func<string, object?[]?, string>? OnInvoke { get; set; }

        public bool Disposed { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (OnInvoke == null)
            {
                throw new InvalidOperationException("FakeJSObjectReference.OnInvoke was not set.");
            }

            var result = OnInvoke(identifier, args);
            return new ValueTask<TValue>((TValue)(object)result);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
