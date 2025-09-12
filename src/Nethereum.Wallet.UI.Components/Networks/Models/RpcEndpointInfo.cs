using CommunityToolkit.Mvvm.ComponentModel;

namespace Nethereum.Wallet.UI.Components.Networks.Models
{
    public partial class RpcEndpointInfo : ObservableObject
    {
        public RpcEndpointInfo(string url, bool isWebSocket = false)
        {
            Url = url;
            IsWebSocket = isWebSocket;
        }
        
        [ObservableProperty] private string _url = "";
        [ObservableProperty] private bool _isWebSocket;
        [ObservableProperty] private bool _isEnabled = true;
        [ObservableProperty] private bool _isHealthy = true;
        [ObservableProperty] private string? _testResult;
        [ObservableProperty] private bool _isTesting;
        [ObservableProperty] private bool _isCustom;
        
        public string TypeDisplayName => IsWebSocket ? "WebSocket" : "HTTP";
        public string StatusDisplayName => IsEnabled ? "Active" : "Inactive";
        public string HealthDisplayName => IsHealthy ? "Healthy" : (TestResult ?? "Unknown");
    }
}