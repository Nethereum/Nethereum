using System.Collections.Generic;

namespace Nethereum.AppChain.Sequencer.Builder
{
    public enum TrustModel
    {
        Open,
        Whitelist,
        InviteTree,
        Quota
    }

    public class TrustConfig
    {
        public TrustModel Model { get; set; } = TrustModel.Open;

        public string? AdminAddress { get; set; }
        public IReadOnlyList<string>? AllowedAddresses { get; set; }

        public int MaxInvitesPerAccount { get; set; } = 3;
        public int MaxDepth { get; set; } = 10;
        public bool RequireActivation { get; set; } = true;

        public int DailyQuota { get; set; } = 100;

        public static TrustConfig Open() => new() { Model = TrustModel.Open };

        public static TrustConfig Whitelist(string admin, IReadOnlyList<string>? allowed = null) => new()
        {
            Model = TrustModel.Whitelist,
            AdminAddress = admin,
            AllowedAddresses = allowed
        };

        public static TrustConfig InviteTree(int maxInvites = 3, int maxDepth = 10, bool requireActivation = true) => new()
        {
            Model = TrustModel.InviteTree,
            MaxInvitesPerAccount = maxInvites,
            MaxDepth = maxDepth,
            RequireActivation = requireActivation
        };

        public static TrustConfig QuotaBased(int dailyQuota = 100) => new()
        {
            Model = TrustModel.Quota,
            DailyQuota = dailyQuota
        };
    }
}
