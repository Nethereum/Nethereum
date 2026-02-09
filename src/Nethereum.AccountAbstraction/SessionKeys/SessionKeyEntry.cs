namespace Nethereum.AccountAbstraction.SessionKeys
{
    public class SessionKeyEntry
    {
        public string Key { get; set; } = null!;
        public string PrivateKey { get; set; } = null!;
        public string AccountAddress { get; set; } = null!;
        public ulong ValidAfter { get; set; }
        public ulong ValidUntil { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset RegisteredAt { get; set; }

        public bool IsValidAt(ulong timestamp) => IsActive && timestamp >= ValidAfter && timestamp <= ValidUntil;
        public bool IsValidNow() => IsValidAt((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public class GeneratedSessionKey
    {
        public string Key { get; set; } = null!;
        public string PrivateKey { get; set; } = null!;
        public string AccountAddress { get; set; } = null!;
        public ulong ValidAfter { get; set; }
        public ulong ValidUntil { get; set; }
        public DateTimeOffset GeneratedAt { get; set; }
    }
}
