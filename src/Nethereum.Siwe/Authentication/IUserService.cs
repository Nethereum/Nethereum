using Nethereum.Siwe.Model;
using Newtonsoft.Json;
using System.Net.Http;
using System;
using System.Threading.Tasks;

namespace Nethereum.Siwe.Authentication
{
    public interface IUserService<TUser> where TUser : User
    {
        Task<TUser> GetUserAsync(string address);
    }
}
