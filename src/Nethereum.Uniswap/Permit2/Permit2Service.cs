using Nethereum.Signer;
using Nethereum.Uniswap.Core.Permit2.ContractDefinition;
using System.Threading.Tasks;

namespace Nethereum.Uniswap.Permit2
{

    public partial class Permit2Service
    {
        public async Task<SignedPermit2<PermitSingle>> GetSinglePermitWithSignatureAsync(PermitSingle permitSingle, EthECKey key)
        {
            var chainId = await this.Web3.Eth.ChainId.SendRequestAsync();
            var verifyingContract = this.ContractHandler.ContractAddress;
            var addressSigner = key.GetPublicAddress();
            var allowance = await this.AllowanceQueryAsync(addressSigner, permitSingle.Details.Token, permitSingle.Spender);
            permitSingle.Details.Nonce = allowance.Nonce;
            var signature = PermitSigner.SignPermitSingle(chainId, verifyingContract, permitSingle, key);
            return new SignedPermit2<PermitSingle> { PermitRequest = permitSingle, Signature = signature };
        }

        public async Task<SignedPermit2<PermitBatch>> GetBatchPermitWithSignatureAsync(PermitBatch permitBatch, EthECKey key)
        {
            var chainId = await this.Web3.Eth.ChainId.SendRequestAsync();
            var verifyingContract = this.ContractHandler.ContractAddress;
            var addressSigner = key.GetPublicAddress();
            for (int i = 0; i < permitBatch.Details.Count; i++)
            {
                var allowance = await this.AllowanceQueryAsync(addressSigner, permitBatch.Details[i].Token, permitBatch.Spender);
                permitBatch.Details[i].Nonce = allowance.Nonce;
            }
            var signature = PermitSigner.SignPermitBatch(chainId, verifyingContract, permitBatch, key);
            return new SignedPermit2<PermitBatch> { PermitRequest = permitBatch, Signature = signature };
        }

    }
}
