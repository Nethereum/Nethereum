using Nethereum.Pantheon.RPC.Permissioning;

namespace Nethereum.Pantheon
{
    public interface IPermissioningApiService
    {
        IPermAddAccountsToWhitelist AddAccountsToWhitelist { get; }
        IPermAddNodesToWhitelist AddNodesToWhitelist { get; }
        IPermRemoveAccountsFromWhitelist RemoveAccountsFromWhitelist { get; }
        IPermRemoveNodesFromWhitelist RemoveNodesFromWhitelist { get; }
        IPermGetAccountsWhitelist GetAccountsWhitelist { get; }
        IPermGetNodesWhitelist GetNodesWhitelist { get; }
        IPermReloadPermissionsFromFile ReloadPermissionsFromFile { get; }
    }
}