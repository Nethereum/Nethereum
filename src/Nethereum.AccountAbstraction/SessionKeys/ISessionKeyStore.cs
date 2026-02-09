namespace Nethereum.AccountAbstraction.SessionKeys
{
    public interface ISessionKeyStore
    {
        Task SaveAsync(SessionKeyEntry entry);
        Task<SessionKeyEntry?> LoadAsync(string keyAddress);
        Task<SessionKeyEntry[]> LoadAllAsync();
        Task DeleteAsync(string keyAddress);
    }
}
