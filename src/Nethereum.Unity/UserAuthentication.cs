using System;
using System.Text;

namespace Nethereum.JsonRpc.UnityClient
{
    public static class UserAuthentication
    {
        public static string GetBasicAuthentication(string userName, string password)
        {
            var byteArray = Encoding.UTF8.GetBytes(userName + ":" + password);
            return "Basic " + Convert.ToBase64String(byteArray);
        }

        public static void SetBasicAuthenticationHeader<T>(this UnityRpcClient<T> unityRpcClient, string userName, string password)
        {
            unityRpcClient.RequestHeaders.Add("AUTHORIZATION", GetBasicAuthentication(userName, password) );
        }
    }
}