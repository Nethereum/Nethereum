#if NET5_0_OR_GREATER
using Nethereum.Siwe.Model;
using System.Net.Http;
using System;
using System.Threading.Tasks;
using Nethereum.Siwe.Core;
using Nethereum.Util.Rest;
using System.Net.Http.Json;

namespace Nethereum.Siwe.Authentication
{
    /// <summary>
    /// Simple Opiniated User Service for the Siwe API
    /// </summary>
    /// <typeparam name="TUser"></typeparam>
    public class SiweApiUserLoginService<TUser> where TUser : User
    {
        private readonly HttpClient _httpClient;

        public string NewSiweMessagePath { get; }
        public string AuthenticatePath { get; }
        public string GetUserPath { get; }
        public string LogoutPath { get; }

        public class AuthenticateRequest
        {
            public string SiweEncodedMessage { get; set; }
            public string Signature { get; set; }
        }

        public class AuthenticateResponse
        {
            public string Address { get; set; }
            public string Jwt { get; set; }
        }

        public SiweApiUserLoginService(HttpClient httpClient, 
                                                        string newSiweMessagePath = "authentication/newsiwemessage",
                                                        string authenticatePath = "authentication/authenticate",
                                                        string getUserPath = "authentication/getuser",
                                                        string logoutPath = "authentication/logout")
        {
            _httpClient = httpClient;
            NewSiweMessagePath = newSiweMessagePath;
            AuthenticatePath = authenticatePath;
            GetUserPath = getUserPath;
            LogoutPath = logoutPath;
           
        }

        public SiweApiUserLoginService(string baseUrl,  string newSiweMessagePath = "authentication/newsiwemessage", 
                                                        string authenticatePath = "authentication/authenticate", 
                                                        string getUserPath = "authentication/getuser", 
                                                        string logoutPath = "authentication/logout")
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(baseUrl);
            NewSiweMessagePath = newSiweMessagePath;
            AuthenticatePath = authenticatePath;
            GetUserPath = getUserPath;
            LogoutPath = logoutPath;
        }

        public async Task<string> GenerateNewSiweMessage(string ethereumAddress)
        {

            var httpMessageResponse = await _httpClient.PostAsync(NewSiweMessagePath, JsonContent.Create(ethereumAddress));
            var message = await httpMessageResponse.Content.ReadAsStringAsync();
            return message;
        }


        public async Task<AuthenticateResponse> Authenticate(SiweMessage siweMessage, string signature)
        {
            var siweMessageEncoded = SiweMessageStringBuilder.BuildMessage(siweMessage);
            var request = new AuthenticateRequest()
            {
                SiweEncodedMessage = siweMessageEncoded,
                Signature = signature
            };

            var httpMessageResponse = await _httpClient.PostAsJsonAsync(AuthenticatePath, request);

            return await httpMessageResponse.Content.ReadFromJsonAsync<AuthenticateResponse>();
        }

        public async Task<TUser> GetUser(string token)
        {
            try
            {
                var user = await _httpClient.GetAsync<TUser>(GetUserPath, token);
                return user;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task Logout(string token)
        {
            await _httpClient.PostAsync(LogoutPath, null, token);

        }
    }
}
#endif