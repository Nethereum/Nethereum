using Nethereum.GSN.Models;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.GSN
{
    public class RelayClient : IRelayClient
    {
        private readonly int _httpTimeout;

        public RelayClient(int httpTimeout)
        {
            _httpTimeout = httpTimeout;
        }

        public async Task<GetAddrResponse> GetAddrAsync(Uri relayUrl)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = relayUrl;
                client.Timeout = TimeSpan.FromMilliseconds(_httpTimeout);

                var result = await client.PostAsync("/getaddr", new StringContent("{}"));
                result.EnsureSuccessStatusCode();

                string content = await result.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<GetAddrResponse>(content);
            }
        }

        public async Task<RelayResponse> RelayAsync(Uri relayUrl, RelayRequest request)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = relayUrl;
                client.Timeout = TimeSpan.FromMilliseconds(_httpTimeout);

                var content = JsonConvert.SerializeObject(request);
                var stringContent = new StringContent(content, Encoding.UTF8, "application/json");

                var result = await client.PostAsync("/relay", stringContent);
                result.EnsureSuccessStatusCode();

                string contentResult = await result.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<RelayResponse>(contentResult);
            }
        }
    }
}
