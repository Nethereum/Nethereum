using System.Numerics;

namespace Nethereum.Contracts.Standards.ERC721
{
    public class ERC721TokenOwnerInfo
    {
        public string Owner { get; set; }
        public string ContractAddress { get; set; }
        public BigInteger TokenId { get; set; }
        public string MetadataUrl { get; set; }
    }
}