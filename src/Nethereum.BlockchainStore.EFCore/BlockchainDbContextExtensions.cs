using Microsoft.EntityFrameworkCore;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.Hex.HexTypes;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.BlockchainStore.EFCore
{
    public static class BlockchainDbContextExtensions
    {
        public static async Task<T> FindByBlockNumberAndHashAsync<T>(this DbSet<T> transactions, HexBigInteger blockNumber, string transactionHash)
            where T: TransactionBase
        {
            var blockNum = (long)blockNumber.Value;
            return await transactions
                .SingleOrDefaultAsync(t => t.BlockNumber == blockNum &&
                                           t.Hash == transactionHash);
        }

        public static async Task<T> FindByBlockNumberAndHashAndAddressAsync<T>(this DbSet<T> transactions, HexBigInteger blockNumber, string transactionHash, string address)
            where T: AddressTransaction
        {
            var blockNum = (long)blockNumber.Value;
            return await transactions
                .SingleOrDefaultAsync(t => t.BlockNumber == blockNum &&
                                           t.Hash == transactionHash && t.Address == address);
        }

        public static async Task<Block> FindByBlockNumberAsync(this DbSet<Block> blocks, HexBigInteger blockNumber)
        {
            var blockNum = (long)blockNumber.Value;
            return await blocks
                .SingleOrDefaultAsync(t => t.BlockNumber == blockNum);
        }

        public static async Task<Contract> FindByContractAddressAsync(this DbSet<Contract> contracts, string contractAddress)
        {
            return await contracts
                .SingleOrDefaultAsync(c => c.Address == contractAddress);
        }

        public static async Task<TransactionLog> FindByTransactionHashAndLogIndexAsync(this DbSet<TransactionLog> transactionLogs, string transactionHash, BigInteger logIndex)
        {
            var idx = (long)logIndex;
            return await transactionLogs
                .SingleOrDefaultAsync(t => t.TransactionHash == transactionHash && t.LogIndex == idx);
        }

        public static async Task<TransactionVmStack> FindByTransactionHashAync(
            this DbSet<TransactionVmStack> transactionVmStacks, string transactionHash)
        {
            return await transactionVmStacks
                .SingleOrDefaultAsync(t => t.TransactionHash == transactionHash);
        }

        public static async Task<TransactionVmStack> FindByAddressAndTransactionHashAync(
            this DbSet<TransactionVmStack> transactionVmStacks, string address, string transactionHash)
        {
            return await transactionVmStacks
                .SingleOrDefaultAsync(t => t.TransactionHash == transactionHash && t.Address == address);
        }

    }
}