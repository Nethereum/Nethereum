namespace Nethereum.Model
{
    public interface IBlockEncodingProvider
    {
        byte[] EncodeReceipt(Receipt receipt);
        byte[] EncodeBlockHeader(BlockHeader header);
        byte[] EncodeAccount(Account account);
        byte[] EncodeLog(Log log);
        byte[] EncodeWithdrawal(ulong index, ulong validatorIndex, byte[] address, ulong amountInGwei);
    }
}
