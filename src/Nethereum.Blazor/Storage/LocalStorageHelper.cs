using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Nethereum.Blazor.Storage
{
    public class LocalStorageHelper
    {
        private readonly IJSRuntime _jsRuntime;

        public LocalStorageHelper(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// Retrieves a value from local storage.
        /// </summary>
        public async Task<string?> GetItemAsync(string key)
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Stores a value in local storage.
        /// </summary>
        public async Task SetItemAsync(string key, string value)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
            }
            catch { }
        }

        /// <summary>
        /// Removes a key from local storage.
        /// </summary>
        public async Task RemoveItemAsync(string key)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
            }
            catch { }
        }
    }
}

