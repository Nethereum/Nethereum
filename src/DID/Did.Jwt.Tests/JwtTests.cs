
using JWT.Algorithms;
using JWT.Builder;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Org.BouncyCastle.Crypto.Digests;
using System;
using System.Collections.Generic;
using Xunit;

namespace Did.Jwt.Tests
{
    public class JwtTests
    {
        static string audAddress = "0x20c769ec9c0996ba7737a4826c2aaff00b1b2040";
        static string aud = $"did:ethr:{audAddress}";
        static string address = "0xf3beac30c498d9e26865f34fcaa57dbb935b0d74";
        static string did = $"did:ethr:{address}";
        static string alg = "ES256K";
        static string privateKey = "278a5de700e29faae8e40e366ec5012b5ec63d36ec77e8a2417154cc1d25383f";
        //static string privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";

        static string publicKey = "03fdd57adec3d438ea237fe46b33ee1e016eda6b585c3e27ea66686c2ea5358479";
        //03fdd57adec3d438ea237fe46b33ee1e016eda6b585c3e27ea66686c2ea5358479
        static int NOW = 1485321133;
        static int mockDate = (NOW * 1000 + 123);

        //this is using ethereum recovery v
        public class ES256KRAlgorithm: IJwtAlgorithm
        {
    
            public string Name => "ES256K-R";

            public byte[] Sign(byte[] key, byte[] bytesToSign)
            {
                var messageSigner = new MessageSigner();
                var hash256 = CalculateHash(bytesToSign);
                var ecKey = new EthECKey(key, true);
                return messageSigner.Sign(hash256, ecKey).HexToByteArray();
            }

            public byte[] CalculateHash(byte[] value)
            {
                var digest = new Sha256Digest();
                var output = new byte[digest.GetDigestSize()];
                digest.BlockUpdate(value, 0, value.Length);
                digest.DoFinal(output, 0);
                return output;
            }
        }

        [Fact]
        public void ItShouldDecodeAndVerify()
        {

            var token = new JwtBuilder().Issuer(did)
                                        .AddClaim("requested", new string[] { "name", "phone" })
                                        .WithAlgorithm(new ES256KRAlgorithm())
                                        .WithSecret(privateKey.HexToByteArray()).Encode();

            var json = new JwtBuilder()
                 .WithAlgorithm(new ES256KRAlgorithm()) // symmetric
                 .WithSecret(privateKey.HexToByteArray())
                 .MustVerifySignature()
                 .Decode(token);

        }

        [Fact]
        public void ItShouldDecodeHeader()
        {
            var token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJFUzI1NksifQ.eyJpYXQiOjE0ODUzMjExMzMsImlzcyI6ImRpZDpldGhyOjB4OTBlNDVkNzViZDEyNDZlMDkyNDg3MjAxODY0N2RiYTk5NmE4ZTdiOSIsInJlcXVlc3RlZCI6WyJuYW1lIiwicGhvbmUiXX0.KIG2zUO8Quf3ucb9jIncZ1CmH0v-fAZlsKvesfsd9x4RzU0qrvinVd9d30DOeZOwdwEdXkET_wuPoOECwU0IKA";
            var header = new JwtBuilder().DecodeHeader<JwtHeader>(token);
            Assert.Equal("JWT", header.Type);
            Assert.Equal("ES256K", header.Algorithm);
            //Assert.Equal("")
            var payload = new JwtBuilder().Decode<IDictionary<string, object>>(token);
            Assert.Equal("did:ethr:0x90e45d75bd1246e0924872018647dba996a8e7b9", payload["iss"]);
            Assert.Equal(new string[] { "name", "phone" }, payload["requested"]);
            Assert.Equal("1485321133", payload["iat"]);
        }

  

        //{"profile":{"did":"did:ethr:0x14535cc684090211874449d9002ed539ba1df066","boxPub":"UpJADJURmBm3Szw7rIg6WCApiQepR+x9T3hTBV+hTFE=","name":"juan blanco","Uportlandia City ID":{"province":"ThatOne","toc":true,"country":"TT","dob":"2007-06-03","city":"Here","lastName":"You","address":"There","firstName":"Me","zipCode":"Me12"},"pushToken":"eyJ0eXAiOiJKV1QiLCJhbGciOiJFUzI1NkstUiJ9.eyJpYXQiOjE1OTI4MjUzOTQsImV4cCI6MTYyNDM2MTM5NCwiYXVkIjoiZGlkOmV0aHI6MHhmMjUzNTc1NzlmNjRlYjE0YjZiZGZlZmJjNzUyYmVhN2M3NzgxOWExIiwidHlwZSI6Im5vdGlmaWNhdGlvbnMiLCJ2YWx1ZSI6ImFybjphd3M6c25zOnVzLXdlc3QtMjoxMTMxOTYyMTY1NTg6ZW5kcG9pbnQvR0NNL3VQb3J0L2ZmZTdlYmRkLWNlZGYtMzYxOS05MjZiLTE5MGFjNWEwZTI4MiIsImlzcyI6ImRpZDpldGhyOjB4MTQ1MzVjYzY4NDA5MDIxMTg3NDQ0OWQ5MDAyZWQ1MzliYTFkZjA2NiJ9.k3omjDkhJukQZhTQi2sanDUQ7ZH61LzwsdrfqVckT1rhCU8_3-Nz2NRdj2xlO6PBZmlm7NMe4FPm80fDVLj2GwA","verified":[{"iat":1592306975,"sub":"did:ethr:0x14535cc684090211874449d9002ed539ba1df066","claim":{"Uportlandia City ID":{"firstName":"Me","lastName":"You","address":"There","city":"Here","province":"ThatOne","zipCode":"Me12","country":"TT","dob":"2007-06-03","toc":true}},"vc":["/ipfs/QmdWnsgD9NuQcBauU8eArxRMCkbDQ42q8miChb6woHmRTR"],"callbackUrl":"https://api.uport.me/chasqui/topic/_8933dF0O","iss":"did:ethr:0xab258a17256ccedb922d680a5dd204ba6b981f09","jwt":"eyJ0eXAiOiJKV1QiLCJhbGciOiJFUzI1NkstUiJ9.eyJpYXQiOjE1OTIzMDY5NzUsInN1YiI6ImRpZDpldGhyOjB4MTQ1MzVjYzY4NDA5MDIxMTg3NDQ0OWQ5MDAyZWQ1MzliYTFkZjA2NiIsImNsYWltIjp7IlVwb3J0bGFuZGlhIENpdHkgSUQiOnsiZmlyc3ROYW1lIjoiTWUiLCJsYXN0TmFtZSI6IllvdSIsImFkZHJlc3MiOiJUaGVyZSIsImNpdHkiOiJIZXJlIiwicHJvdmluY2UiOiJUaGF0T25lIiwiemlwQ29kZSI6Ik1lMTIiLCJjb3VudHJ5IjoiVFQiLCJkb2IiOiIyMDA3LTA2LTAzIiwidG9jIjp0cnVlfX0sInZjIjpbIi9pcGZzL1FtZFduc2dEOU51UWNCYXVVOGVBcnhSTUNrYkRRNDJxOG1pQ2hiNndvSG1SVFIiXSwiY2FsbGJhY2tVcmwiOiJodHRwczovL2FwaS51cG9ydC5tZS9jaGFzcXVpL3RvcGljL184OTMzZEYwTyIsImlzcyI6ImRpZDpldGhyOjB4YWIyNThhMTcyNTZjY2VkYjkyMmQ2ODBhNWRkMjA0YmE2Yjk4MWYwOSJ9.mBDl9YvqFcJgkTqgjnrhKXvFlt_O5_SOI-lO2mq_ptXXgKSha4VFsFnGdT1QrlrsAM7c1FS9AXTWLHbE5YLy7AA"}],"invalid":[],"publicEncKey":"UpJADJURmBm3Szw7rIg6WCApiQepR+x9T3hTBV+hTFE="}}

//Request verified data job application
/*
 * {
"iat": 1592825841,
"exp": 1592825961,
"requested": [
"name"
],
"verified": [
"Uportlandia City ID",
"Diploma"
],
"permissions": [
"notifications"
],
"callback": "https://api.uport.me/chasqui/topic/zeVczyt10",
"vc": [
"/ipfs/QmRnfAn98Y4QfNvZje8hiSextdY6uPhiAmRLidQJwChUZo"
],
"act": "none",
"type": "shareReq",
"iss": 

    */

}
}
