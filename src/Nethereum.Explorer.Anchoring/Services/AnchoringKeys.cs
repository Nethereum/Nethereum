namespace Nethereum.Explorer.Anchoring.Services
{
    public static class AnchoringKeys
    {
        public static class Nav
        {
            public const string Anchoring = "Anchoring.Nav.Anchoring";
        }

        public static class Dashboard
        {
            public const string Title = "Anchoring.Dashboard.Title";
            public const string Chains = "Anchoring.Dashboard.Chains";
            public const string NotConfigured = "Anchoring.Dashboard.NotConfigured";
            public const string NoChains = "Anchoring.Dashboard.NoChains";
            public const string NoChainsSub = "Anchoring.Dashboard.NoChainsSub";
            public const string Chain = "Anchoring.Dashboard.Chain";
            public const string ViewDetails = "Anchoring.Dashboard.ViewDetails";
        }

        public static class Detail
        {
            public const string Title = "Anchoring.Detail.Title";
            public const string AllChains = "Anchoring.Detail.AllChains";
            public const string AnchorHistory = "Anchoring.Detail.AnchorHistory";
            public const string Anchors = "Anchoring.Detail.Anchors";
        }

        public static class Stats
        {
            public const string LatestBlock = "Anchoring.Stats.LatestBlock";
            public const string TotalAnchors = "Anchoring.Stats.TotalAnchors";
            public const string ProvenBlocks = "Anchoring.Stats.ProvenBlocks";
            public const string ProofSystem = "Anchoring.Stats.ProofSystem";
            public const string AvgInterval = "Anchoring.Stats.AvgInterval";
        }

        public static class Admin
        {
            public const string Title = "Anchoring.Admin.Title";
            public const string Status = "Anchoring.Admin.Status";
            public const string Strategy = "Anchoring.Admin.Strategy";
            public const string DataAvailability = "Anchoring.Admin.DataAvailability";
            public const string ProofMode = "Anchoring.Admin.ProofMode";
            public const string Cadence = "Anchoring.Admin.Cadence";
            public const string Interval = "Anchoring.Admin.Interval";
            public const string ForceAnchor = "Anchoring.Admin.ForceAnchor";
            public const string ApplyStrategy = "Anchoring.Admin.ApplyStrategy";
            public const string SaveConfig = "Anchoring.Admin.SaveConfig";
            public const string Initializing = "Anchoring.Admin.Initializing";
            public const string Running = "Anchoring.Admin.Running";
            public const string Stopped = "Anchoring.Admin.Stopped";
            public const string AvailableStrategies = "Anchoring.Admin.AvailableStrategies";
            public const string CurrentConfig = "Anchoring.Admin.CurrentConfig";
            public const string AnchorContract = "Anchoring.Admin.AnchorContract";
            public const string Blocks = "Anchoring.Admin.Blocks";
            public const string Milliseconds = "Anchoring.Admin.Milliseconds";
        }

        public static class Table
        {
            public const string Blocks = "Anchoring.Table.Blocks";
            public const string EndBlockHash = "Anchoring.Table.EndBlockHash";
            public const string StateRoot = "Anchoring.Table.StateRoot";
            public const string Proof = "Anchoring.Table.Proof";
            public const string MainchainTx = "Anchoring.Table.MainchainTx";
            public const string Operator = "Anchoring.Table.Operator";
            public const string ProofSize = "Anchoring.Table.ProofSize";
        }
    }
}
