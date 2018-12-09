using Nethereum.RPC.Personal;

namespace Nethereum.RPC
{
    public interface IPersonalApiService
    {
        IPersonalListAccounts ListAccounts { get; }
        IPersonalLockAccount LockAccount { get; }
        IPersonalNewAccount NewAccount { get; }
        IPersonalSignAndSendTransaction SignAndSendTransaction { get; }
        IPersonalUnlockAccount UnlockAccount { get; }
    }
}