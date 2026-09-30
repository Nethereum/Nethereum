namespace Nethereum.Explorer.Services
{
    public class ExplorerNavItem
    {
        public string Label { get; init; } = "";
        public string Href { get; init; } = "";
        public string IconClass { get; init; } = "";
        public int Order { get; init; }
    }

    public interface IExplorerNavContributor
    {
        IReadOnlyList<ExplorerNavItem> GetNavItems();
    }
}
