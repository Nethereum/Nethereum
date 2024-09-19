#if !DOTNET35
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nethereum.Util.Rest
{
    public interface IRestHttpHelper
    {
        Task<T> GetAsync<T>(string path, Dictionary<string, string> headers = null);
        Task<TResponse> PostAsync<TResponse, TRequest>(string path, TRequest request,  Dictionary<string, string> headers = null);
        Task<TResponse> PutAsync<TResponse, TRequest>(string path, TRequest request, Dictionary<string, string> headers = null);
        Task DeleteAsync(string path, Dictionary<string, string> headers = null);
    }

}
#endif