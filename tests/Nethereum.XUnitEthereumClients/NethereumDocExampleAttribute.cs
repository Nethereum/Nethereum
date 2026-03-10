using System;

namespace Nethereum.XUnitEthereumClients
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
        MudFramework,
        WalletUI,
        Consensus,
        ClientExtensions
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
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
            return Section switch
            {
                DocSection.CoreFoundation => "core-foundation",
                DocSection.Signing => "signing",
                DocSection.SmartContracts => "smart-contracts",
                DocSection.DeFi => "defi",
                DocSection.EvmSimulator => "evm-simulator",
                DocSection.InProcessNode => "in-process-node",
                DocSection.AccountAbstraction => "account-abstraction",
                DocSection.DataIndexing => "data-indexing",
                DocSection.MudFramework => "mud-framework",
                DocSection.WalletUI => "wallet-ui",
                DocSection.Consensus => "consensus",
                DocSection.ClientExtensions => "client-extensions",
                _ => Section.ToString().ToLowerInvariant()
            };
        }
    }
}
