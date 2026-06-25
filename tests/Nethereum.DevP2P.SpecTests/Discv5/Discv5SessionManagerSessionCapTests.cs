using System;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    /// <summary>
    /// Per the reference implementation, the session cache is bounded — a
    /// flood of fresh peers must not be able to grow the dictionary without
    /// bound. The oldest session by <see cref="Discv5Session.CreatedUtc"/> is
    /// evicted when the cap is exceeded.
    /// </summary>
    public class Discv5SessionManagerSessionCapTests
    {
        [Fact]
        public void Given_SessionDictAtCap_When_EvictionInvokedViaCapBreach_Then_OldestSessionDropped()
        {
            var localKey = EthECKey.GenerateKey();
            var mgr = new Discv5SessionManager(localKey);

            var dict = GetSessionsDict(mgr);

            // Fill the dictionary up to the cap with synthetic sessions.
            var baseTime = DateTime.UtcNow.AddHours(-1);
            string oldestKey = null;
            for (int i = 0; i < Discv5SessionManager.MaxSessions; i++)
            {
                var session = new Discv5Session
                {
                    RemoteNodeId = MakeNodeId(i),
                    RemoteAddr = new IPEndPoint(IPAddress.Loopback, 30000 + i),
                    InitiatorKey = new byte[16],
                    RecipientKey = new byte[16],
                    IsInitiator = false,
                    CreatedUtc = baseTime.AddSeconds(i),
                };
                var key = $"key{i}";
                if (i == 0) oldestKey = key;
                dict[key] = session;
            }
            Assert.Equal(Discv5SessionManager.MaxSessions, mgr.SessionCount);

            // Use reflection to invoke EvictOldestSessionIfAtCap directly,
            // then add a new entry — total count must still be at or below cap.
            var evict = typeof(Discv5SessionManager).GetMethod(
                "EvictOldestSessionIfAtCap", BindingFlags.NonPublic | BindingFlags.Instance);
            evict.Invoke(mgr, null);

            Assert.False(dict.ContainsKey(oldestKey),
                "oldest synthetic session should have been evicted");
            Assert.Equal(Discv5SessionManager.MaxSessions - 1, mgr.SessionCount);
        }

        private static ConcurrentDictionary<string, Discv5Session> GetSessionsDict(Discv5SessionManager mgr)
        {
            var field = typeof(Discv5SessionManager).GetField(
                "_sessions", BindingFlags.NonPublic | BindingFlags.Instance);
            return (ConcurrentDictionary<string, Discv5Session>)field.GetValue(mgr);
        }

        private static byte[] MakeNodeId(int seed)
        {
            var b = new byte[32];
            b[0] = (byte)(seed & 0xff);
            b[1] = (byte)((seed >> 8) & 0xff);
            return b;
        }
    }
}
