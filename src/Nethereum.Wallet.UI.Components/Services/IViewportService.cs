using System;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Services
{
    public interface IViewportService
    {
        int Width { get; }
        int Height { get; }
        bool IsCompact { get; }
        event EventHandler<ViewportChangedEventArgs>? ViewportChanged;
        Task InitializeAsync();
        Task UpdateViewportAsync(int width, int height);
        void Dispose();
    }
    public class ViewportChangedEventArgs : EventArgs
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsCompact { get; set; }
        public int PreviousWidth { get; set; }
        public int PreviousHeight { get; set; }
        public bool WasCompact { get; set; }
    }
}