using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.BlockchainProcessing.Storage.Entities.Mapping
{
    public static class ContractMapping
    {

        public static Contract MapToStorageEntityForUpsert(this ContractCreationVO contractCreationVO)
        {
            return contractCreationVO.MapToStorageEntityForUpsert<Contract>();
        }

        public static TEntity MapToStorageEntityForUpsert<TEntity>(this ContractCreationVO contractCreationVO) where TEntity : Contract, new()
        {
            var contract = new TEntity();
            contract.Map(contractCreationVO.ContractAddress, contractCreationVO.Code, contractCreationVO.Transaction);
            contract.UpdateRowDates();
            return contract;
        }

        public static void Map(this Contract contract, string contractAddress, string code, Nethereum.RPC.Eth.DTOs.Transaction transaction)
        {
            contract.Address = contractAddress;
            contract.Code = code;
            contract.TransactionHash = transaction.TransactionHash;
            contract.Creator = transaction.From;
        }
    }
}
