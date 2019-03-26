using System;
using System.Net.Http.Headers;
using System.Text;

namespace Nethereum.JsonRpc.Client
{
    public class UserAuthentication
    {
        public string UserName { get; set; }
        public string Password { get; set; }

        public static UserAuthentication FromUrl(string url)
        {
            return FromUri(new Uri(url));
        }

        public AuthenticationHeaderValue GetBasicAuthenticationHeaderValue()
        {
            var byteArray = Encoding.UTF8.GetBytes(UserName + ":" + Password);
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        public static UserAuthentication FromUri(Uri uri)
        {
            var userInfo = uri.UserInfo?.Split(':');
            UserAuthentication userAuthentication = null;
            if (userInfo != null)
            {
                userAuthentication = new UserAuthentication();
                userAuthentication.UserName = userInfo[0];
                if (userInfo.Length > 1)
                {
                    userAuthentication.Password = userInfo[1];
                }
            }

            return userAuthentication;
        }
    }
}