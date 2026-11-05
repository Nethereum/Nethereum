using System;
using Nethereum.Signer.Crypto;
using Nethereum.Signer.Crypto.BN128;
using Nethereum.ZkProofsVerifier.Groth16;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
#if NET6_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif

namespace Nethereum.ZkProofsVerifier.Circom
{
    public static class SnarkjsVerificationKeyParser
    {
        public static Groth16VerificationKey Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("Verification key JSON is null or empty");

#if NET6_0_OR_GREATER
            return ParseWithStj(json);
#else
            return ParseWithNewtonsoft(json);
#endif
        }

#if NET6_0_OR_GREATER
        private static Groth16VerificationKey ParseWithStj(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var alpha = ParseG1Stj(root.GetProperty("vk_alpha_1"));
            var beta = ParseG2Stj(root.GetProperty("vk_beta_2"));
            var gamma = ParseG2Stj(root.GetProperty("vk_gamma_2"));
            var delta = ParseG2Stj(root.GetProperty("vk_delta_2"));

            var icArray = root.GetProperty("IC");
            var ic = new ECPoint[icArray.GetArrayLength()];
            for (int i = 0; i < ic.Length; i++)
            {
                ic[i] = ParseG1Stj(icArray[i]);
            }

            return new Groth16VerificationKey
            {
                Alpha = alpha,
                Beta = beta,
                Gamma = gamma,
                Delta = delta,
                IC = ic
            };
        }

        private static ECPoint ParseG1Stj(JsonElement arr)
        {
            return Groth16PointValidator.CreateG1(arr[0].GetString(), arr[1].GetString());
        }

        private static TwistPoint ParseG2Stj(JsonElement arr)
        {
            return Groth16PointValidator.CreateG2(arr[0][0].GetString(), arr[0][1].GetString(), arr[1][0].GetString(), arr[1][1].GetString());
        }
#else
        private static Groth16VerificationKey ParseWithNewtonsoft(string json)
        {
            var obj = JObject.Parse(json);

            var alpha = ParseG1Newtonsoft((JArray)obj["vk_alpha_1"]);
            var beta = ParseG2Newtonsoft((JArray)obj["vk_beta_2"]);
            var gamma = ParseG2Newtonsoft((JArray)obj["vk_gamma_2"]);
            var delta = ParseG2Newtonsoft((JArray)obj["vk_delta_2"]);

            var icArray = (JArray)obj["IC"];
            var ic = new ECPoint[icArray.Count];
            for (int i = 0; i < ic.Length; i++)
            {
                ic[i] = ParseG1Newtonsoft((JArray)icArray[i]);
            }

            return new Groth16VerificationKey
            {
                Alpha = alpha,
                Beta = beta,
                Gamma = gamma,
                Delta = delta,
                IC = ic
            };
        }

        private static ECPoint ParseG1Newtonsoft(JArray arr)
        {
            return Groth16PointValidator.CreateG1((string)arr[0], (string)arr[1]);
        }

        private static TwistPoint ParseG2Newtonsoft(JArray arr)
        {
            return Groth16PointValidator.CreateG2((string)((JArray)arr[0])[0], (string)((JArray)arr[0])[1], (string)((JArray)arr[1])[0], (string)((JArray)arr[1])[1]);
        }
#endif
    }
}
