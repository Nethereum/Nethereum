using System;
using System.Threading.Tasks;
using Nethereum.Wallet.UI.Components.Configuration;

namespace Nethereum.Wallet.UI.Components.Services
{
    public class ViewportService : IViewportService, IDisposable
    {
        private readonly INethereumWalletUIConfiguration _configuration;
        private int _width = 1024;
        private int _height = 768;
        private bool _disposed = false;
        
        public ViewportService(INethereumWalletUIConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        public int Width => _width;
        public int Height => _height;
        public bool IsCompact => _width < _configuration.ResponsiveBreakpoint;
        
        public event EventHandler<ViewportChangedEventArgs>? ViewportChanged;
        
        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }
        
        public async Task UpdateViewportAsync(int width, int height)
        {
            if (_disposed) return;
            
            var previousWidth = _width;
            var previousHeight = _height;
            var wasCompact = IsCompact;
            
            if (Math.Abs(width - _width) > 5 || Math.Abs(height - _height) > 5)
            {
                _width = width;
                _height = height;
                
                var args = new ViewportChangedEventArgs
                {
                    Width = _width,
                    Height = _height,
                    IsCompact = IsCompact,
                    PreviousWidth = previousWidth,
                    PreviousHeight = previousHeight,
                    WasCompact = wasCompact
                };
                
                ViewportChanged?.Invoke(this, args);
                await Task.Delay(1);
                
                System.Console.WriteLine($"ViewportService: Updated to {_width}x{_height}, IsCompact={IsCompact}, Breakpoint={_configuration.ResponsiveBreakpoint}");
            }
        }
        
        public void Dispose()
        {
            _disposed = true;
            ViewportChanged = null;
        }
    }
}