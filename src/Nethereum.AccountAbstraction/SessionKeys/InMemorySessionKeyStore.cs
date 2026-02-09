namespace Nethereum.AccountAbstraction.SessionKeys
{
    public class InMemorySessionKeyStore : ISessionKeyStore
    {
        private readonly Dictionary<string, SessionKeyEntry> _entries = new();

        public Task SaveAsync(SessionKeyEntry entry)
        {
            _entries[entry.Key.ToLowerInvariant()] = entry;
            return Task.CompletedTask;
        }

        public Task<SessionKeyEntry?> LoadAsync(string keyAddress)
        {
            _entries.TryGetValue(keyAddress.ToLowerInvariant(), out var entry);
            return Task.FromResult(entry);
        }

        public Task<SessionKeyEntry[]> LoadAllAsync()
        {
            return Task.FromResult(_entries.Values.ToArray());
        }

        public Task DeleteAsync(string keyAddress)
        {
            _entries.Remove(keyAddress.ToLowerInvariant());
            return Task.CompletedTask;
        }
    }
}
