using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM.Gas.Intrinsic
{
    public abstract class BlobGasRule : IBlobGasRule
    {
        private readonly EvmUInt256 _updateFraction;

        protected BlobGasRule(int baseFeeUpdateFraction, bool appliesReservePrice = false)
        {
            _updateFraction = new EvmUInt256(baseFeeUpdateFraction);
            AppliesReservePrice = appliesReservePrice;
        }

        public EvmUInt256 BaseFeeUpdateFraction => _updateFraction;

        public bool AppliesReservePrice { get; }

        public EvmUInt256 CalculateBlobBaseFee(EvmUInt256 excessBlobGas)
        {
            return BlobGasCalculator.CalculateBlobBaseFee(excessBlobGas, _updateFraction);
        }

        public EvmUInt256 CalculateBlobGasCost(int blobCount, EvmUInt256 blobBaseFee)
        {
            return BlobGasCalculator.CalculateBlobGasCost(blobCount, blobBaseFee);
        }
    }
}
