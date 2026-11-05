using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Web3;
using System.Threading.Tasks;

namespace Nethereum.X402.Blockchain;

internal static class Eip3009TokenDomainReader
{
    [Function("name", "string")]
    private class NameFunction : FunctionMessage
    {
    }

    [Function("version", "string")]
    private class VersionFunction : FunctionMessage
    {
    }

    public static Task<string> NameAsync(IWeb3 web3, string token) => ReadAsync<NameFunction>(web3, token);

    public static Task<string> VersionAsync(IWeb3 web3, string token) => ReadAsync<VersionFunction>(web3, token);

    private static async Task<string> ReadAsync<TFunction>(IWeb3 web3, string token) where TFunction : FunctionMessage, new()
    {
        try
        {
            return await web3.Eth.GetContractQueryHandler<TFunction>()
                .QueryAsync<string>(token, new TFunction())
                .ConfigureAwait(false);
        }
        catch (System.Exception ex) when (ex is not System.OperationCanceledException)
        {
            return null;
        }
    }
}
