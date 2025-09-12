namespace Nethereum.Wallet.UI.Components.Core.Configuration
{
    public interface IComponentConfiguration
    {
        string ComponentId { get; }
    }
    public interface IComponentConfiguration<TComponent> : IComponentConfiguration
    {
    }
}