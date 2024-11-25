namespace Nethereum.Reown.AppKit.Blazor.Wagmi;

internal class GetAccountReturnType {
	public string? Address { get; set; }
	public string[]? Addresses { get; set; }
	public long ChainId { get; set; }
	public bool IsConnecting { get; set; }
	public bool IsReconnecting { get; set; }
	public bool IsConnected { get; set; }
	public bool IsDisconnected { get; set; }
	public string? Status { get; set; }
}