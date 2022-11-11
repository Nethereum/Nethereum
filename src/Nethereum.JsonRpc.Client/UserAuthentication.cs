using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.JsonRpc.WebSocketStreamingClient;

namespace Nethereum.JsonRpc.Client
{

    public interface IClientRequestHeaderSupport
    {
        Dictionary<string, string> RequestHeaders { get; set; }
    } 
    
    public static class UserAuthentication
    {

        public class BasicAuthenticationUserInfo
        {
            public string UserName { get; set; }
            public string Password { get; set; }
        }

        public static string GetBasicAuthentication(string userName, string password)
        {
            var byteArray = Encoding.UTF8.GetBytes(userName + ":" + password);
            return "Basic " + Convert.ToBase64String(byteArray);
        }

       

        public static BasicAuthenticationUserInfo GetBasicAuthenticationUserInfoFromUri(Uri uri)
        {
            if (uri.UserInfo != String.Empty)
            {
                var userInfo = uri.UserInfo?.Split(':');
                if (userInfo.Length == 2)
                {
                    var userName = userInfo[0];
                    var password = userInfo[1];
                    return new BasicAuthenticationUserInfo() {UserName = userName, Password = password};
                }
            }

            return null;
        }

        public static void SetBasicAuthenticationHeaderFromUri(this IClientRequestHeaderSupport client,
            Uri uri)
        {
            var userInfo = GetBasicAuthenticationUserInfoFromUri(uri);
            if(userInfo != null){
                SetBasicAuthenticationHeader(client, userInfo.UserName, userInfo.Password);
            }
        }

        public static void SetBasicAuthenticationHeader(this IClientRequestHeaderSupport client, string userName, string password)
        {
            client.RequestHeaders.Add("AUTHORIZATION", GetBasicAuthentication(userName, password) );
        }

    }
}