using System;

namespace Nethereum.Model.Codecs
{
    public static class BlockHeaderCodecExtensions
    {
        public static void ClearFieldsNotCarried(this IBlockHeaderCodec codec, BlockHeader header)
        {
            if (codec == null) throw new ArgumentNullException(nameof(codec));
            if (header == null) throw new ArgumentNullException(nameof(header));

            if (!codec.CarriesBaseFee) header.BaseFee = null;
            if (!codec.CarriesWithdrawalsRoot) header.WithdrawalsRoot = null;
            if (!codec.CarriesBlobFieldsAndBeaconRoot)
            {
                header.BlobGasUsed = null;
                header.ExcessBlobGas = null;
                header.ParentBeaconBlockRoot = null;
            }
            if (!codec.CarriesRequestsHash) header.RequestsHash = null;
            if (!codec.CarriesBlockAccessList)
            {
                header.BlockAccessListHash = null;
                header.SlotNumber = null;
            }
        }
    }
}
