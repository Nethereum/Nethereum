using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Siwe.Core;
using Xunit;

namespace Nethereum.Siwe.UnitTests
{
    public class InMemorySessionNonceStorageConcurrencyTests
    {
        [Fact]
        public async Task Given_ConcurrentGetAndRemoveOfTheSameNonce_When_Hammered_Then_NoExceptionAndTheResultIsTheMessageOrNull()
        {
            var storage = new InMemorySessionNonceStorage();
            var message = new SiweMessage { Nonce = "abcdefgh" };
            var failures = new ConcurrentBag<Exception>();
            var stop = false;

            var readers = new Task[4];
            for (var i = 0; i < readers.Length; i++)
            {
                readers[i] = Task.Run(() =>
                {
                    while (!Volatile.Read(ref stop))
                    {
                        try
                        {
                            var result = storage.GetSiweMessage(message);
                            if (result != null && !ReferenceEquals(result, message))
                                failures.Add(new InvalidOperationException("unexpected message"));
                        }
                        catch (Exception ex)
                        {
                            failures.Add(ex);
                        }
                    }
                });
            }

            var writer = Task.Run(() =>
            {
                for (var i = 0; i < 200_000; i++)
                {
                    storage.AddOrUpdate(message);
                    storage.Remove(message.Nonce);
                }
            });

            await writer;
            Volatile.Write(ref stop, true);
            await Task.WhenAll(readers);

            Assert.Empty(failures);
            Assert.Null(storage.GetSiweMessage(message));
        }
    }
}
