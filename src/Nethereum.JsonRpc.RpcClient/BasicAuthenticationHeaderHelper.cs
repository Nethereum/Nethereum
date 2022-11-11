using System;
using System.Net.Http.Headers;
using System.Text;

namespace Nethereum.JsonRpc.Client
{
    public class BasicAuthenticationHeaderHelper
    {
        public static AuthenticationHeaderValue GetBasicAuthenticationHeaderValueFromUri(Uri uri)
        {
            var userInfo = UserAuthentication.GetBasicAuthenticationUserInfoFromUri(uri);
            if (userInfo != null)
            {
                var byteArray = Encoding.UTF8.GetBytes(userInfo.UserName + ":" + userInfo.Password);
                return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }

            return null;
        }
    }
}