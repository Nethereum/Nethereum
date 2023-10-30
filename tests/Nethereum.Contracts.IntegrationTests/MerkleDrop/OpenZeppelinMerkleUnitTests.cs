using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Nethereum.Merkle;
using System.Numerics;

namespace Nethereum.Contracts.IntegrationTests.MerkleDrop
{
    public class OpenZeppelinMerkleUnitTests
    {
        [Fact]
        public void SingleParamSingleItemHash()
        {
            var expected = "0x3f4a1640bcca71e45d053d67ab9891fe44608f4db37cc45e5523588c76c79539";
            var item = new SingleParam { User = "0x95222290DD7278Aa3Ddd389Cc1E1d165CC4BAfe5" };

            var merkleTree = new OpenZeppelinStandardMerkleTree<SingleParam>();
            merkleTree.BuildTree(new List<SingleParam> { item });

            var hexRoot = merkleTree.Root.Hash.ToHex(true);

            Assert.True(expected.IsTheSameHex(hexRoot));
        }

        [Fact]
        public void SingleParamMultipleItems()
        {
            var expected = "0xa1b819a3413fb3120a911642ee7fbdb17572e844090a95a0848d07aa230eb702";
            var item1 = new SingleParam { User = "0x95222290DD7278Aa3Ddd389Cc1E1d165CC4BAfe5" };
            var item2 = new SingleParam { User = "0xA61b1fB89Dd42fcDDD2D3fA19c2B715c426692c7" };
            var item3 = new SingleParam { User = "0xfa6179E49EE57a06391F218965b35B632F930472" };
            var item4 = new SingleParam { User = "0x1f9090aaE28b8a3dCeaDf281B0F12828e676c326" };


            var items = new List<SingleParam> { item1, item2, item3, item4 };


            var merkleTree = new OpenZeppelinStandardMerkleTree<SingleParam>();
            merkleTree.BuildTree(items);

            var hexRoot = merkleTree.Root.Hash.ToHex(true);
            Assert.True(expected.IsTheSameHex(hexRoot));
        }

        [Fact]
        public void MultiParam_MultipleItems()
        {
            var expected = "0xd681161482c0d2826015b40ca9e2595230d62d0041dfe7e37d0f5c5fb3d7e647";
            var number = 2;
            var items = new List<MultiParam>
            {
                new MultiParam { Number = 2, User = "0xa22003bf951e9a050d4c7600ac1630888ab8d522" },
                new MultiParam { Number = 2, User = "0xaf2c5c6c4104d62130dd32d442a10f0eb67883db" },
                new MultiParam { Number = 2, User = "0xaf2c5c6c4104d62130dd32d442a10f0eb67883db" },
                new MultiParam { Number = 2, User = "0x2721e2fd9de72950ee16a3356ebf29669d11d325" },
                new MultiParam { Number = 2, User = "0x2721e2fd9de72950ee16a3356ebf29669d11d325" },
                new MultiParam { Number = 2, User = "0x8b4bc5cb23b2b3a2243e44b7812344949943d608" },
                new MultiParam { Number = 2, User = "0x8b4bc5cb23b2b3a2243e44b7812344949943d608" },
                new MultiParam { Number = 2, User = "0xefd394c41d77d38c397e2999470080339e8443f8" },
                new MultiParam { Number = 2, User = "0xefd394c41d77d38c397e2999470080339e8443f8" },
                new MultiParam { Number = 2, User = "0xf2ce910fb11ea4abbf7ff0ecdb80527f12f63165" },
                new MultiParam { Number = 2, User = "0xf2ce910fb11ea4abbf7ff0ecdb80527f12f63165" },
                new MultiParam { Number = 2, User = "0xa22003bf951e9a050d4c7600ac1630888ab8d522" },
            };

            var merkleTree = new OpenZeppelinStandardMerkleTree<MultiParam>();
            merkleTree.BuildTree(items);
            var hexRoot = merkleTree.Root.Hash.ToHex(true);
            Assert.True(expected.IsTheSameHex(hexRoot));
        }

    }

    [Struct("SingleParam")]
    public class SingleParam
    {
        [Parameter("address", 1)]
        public string User { get; set; }
    }

    [Struct("MultiParam")]
    public class MultiParam
    {
        [Parameter("address", 1)]
        public string User { get; set; }

        [Parameter("uint256", "cycle", 2)]
        public BigInteger Number { get; set; }
    }

}
