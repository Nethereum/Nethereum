using System;

namespace Nethereum.Documentation
{
    public enum DocSection
    {
        CoreFoundation,
        Signing,
        SmartContracts,
        DeFi,
        EvmSimulator,
        InProcessNode,
        AccountAbstraction,
        DataIndexing,
        DataServices,
        MudFramework,
        WalletUI,
        Consensus,
        ClientExtensions,
        Protocols
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class NethereumDocExampleAttribute : Attribute
    {
        public DocSection Section { get; }
        public string UseCase { get; }
        public string Title { get; }
        public string SkillName { get; set; }
        public int Order { get; set; }

        public NethereumDocExampleAttribute(DocSection section, string useCase, string title)
        {
            Section = section;
            UseCase = useCase;
            Title = title;
            Order = 0;
        }

        public string GetSectionSlug()
        {
            switch (Section)
            {
                case DocSection.CoreFoundation: return "core-foundation";
                case DocSection.Signing: return "signing";
                case DocSection.SmartContracts: return "smart-contracts";
                case DocSection.DeFi: return "defi";
                case DocSection.EvmSimulator: return "evm-simulator";
                case DocSection.InProcessNode: return "in-process-node";
                case DocSection.AccountAbstraction: return "account-abstraction";
                case DocSection.DataIndexing: return "data-indexing";
                case DocSection.DataServices: return "data-services";
                case DocSection.MudFramework: return "mud-framework";
                case DocSection.WalletUI: return "wallet-ui";
                case DocSection.Consensus: return "consensus";
                case DocSection.ClientExtensions: return "client-extensions";
                case DocSection.Protocols: return "protocols";
                default: return Section.ToString().ToLowerInvariant();
            }
        }
    }
}
