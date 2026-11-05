using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Dns;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class S1bR2DnsRootSignatureSecurityTests
    {
        private static readonly BigInteger Order = BigInteger.Parse(
            "0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141", System.Globalization.NumberStyles.HexNumber);

        private static byte[] Be32(BigInteger value)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            var padded = new byte[32];
            Buffer.BlockCopy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
        }

        private static async Task<int> ResolveWithRootSAsync(bool highS)
        {
            var tree = new SignedEnrTree("nodes.example.org");
            var leaf = tree.AddEnr(EthECKey.GenerateKey());
            var eLabel = tree.AddBranch(leaf);
            var lLabel = tree.AddBranch();
            var content = $"enrtree-root:v1 e={eLabel} l={lLabel} seq=1";
            var digest = new Sha3Keccack().CalculateHash(Encoding.ASCII.GetBytes(content));
            var sig = tree.Key.SignAndCalculateV(digest);
            var s = new BigInteger(sig.S.PadBytes(32), isUnsigned: true, isBigEndian: true);
            var chosenS = highS ? Order - s : s;
            if (highS == (s > Order / 2)) chosenS = s;
            var sigBytes = new byte[65];
            Buffer.BlockCopy(sig.R.PadBytes(32), 0, sigBytes, 0, 32);
            Buffer.BlockCopy(Be32(chosenS), 0, sigBytes, 32, 32);
            var root = content + " sig=" + Convert.ToBase64String(sigBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            var inner = SignedEnrTree.Resolver(tree);
            Func<string, TimeSpan, CancellationToken, Task<List<string>>> resolve = (name, timeout, ct) =>
                name.Equals(tree.Domain, StringComparison.OrdinalIgnoreCase)
                    ? Task.FromResult(new List<string> { root })
                    : inner(name, timeout, ct);
            var resolver = new EnrTreeResolver(null, resolve);

            var enrs = await resolver.ResolveEnrsAsync(tree.Url, TimeSpan.FromSeconds(5), 10, CancellationToken.None);
            return enrs.Count;
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_RootSignedWithTheLowSForm_When_Resolved_Then_TheLeavesAreReturned()
        {
            Assert.Equal(1, await ResolveWithRootSAsync(highS: false));
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_RootSignedWithTheMalleableHighSForm_When_Resolved_Then_TheRootIsRejected()
        {
            Assert.Equal(0, await ResolveWithRootSAsync(highS: true));
        }
    }
}
