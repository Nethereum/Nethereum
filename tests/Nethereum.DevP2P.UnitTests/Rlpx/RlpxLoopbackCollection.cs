using Xunit;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    /// <summary>
    /// Test collection that serialises classes which spin up real loopback TCP
    /// listeners + connections. Running these in parallel with the rest of the
    /// xunit suite consumes ephemeral ports and starves the ThreadPool, causing
    /// handshake timeouts that have nothing to do with the code under test.
    /// </summary>
    [CollectionDefinition("RlpxLoopback", DisableParallelization = true)]
    public class RlpxLoopbackCollection
    {
    }
}
