namespace Nethereum.AccountAbstraction.AppChain.Interfaces
{
    public interface IAccountRegistry
    {
        Task<string> InviteAsync(string account);
        Task<string> ActivateAsync(string account);
        Task<string> RevokeAsync(string account);
        Task<bool> IsInvitedAsync(string account);
        Task<bool> IsActiveAsync(string account);
        Task<AccountStatus> GetStatusAsync(string account);
        Task<bool> IsAdminAsync(string address);
        Task<string> SetAdminAsync(string admin, bool isAdmin);
    }
}
