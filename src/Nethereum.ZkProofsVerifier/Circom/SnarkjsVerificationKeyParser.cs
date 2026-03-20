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
    internal static class SnarkjsVerificationKeyParser
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
            var x = new BigInteger(arr[0].GetString());
            var y = new BigInteger(arr[1].GetString());
            return BN128Curve.Curve.CreatePoint(x, y);
        }

        private static TwistPoint ParseG2Stj(JsonElement arr)
        {
            var xC0 = new BigInteger(arr[0][0].GetString());
            var xC1 = new BigInteger(arr[0][1].GetString());
            var yC0 = new BigInteger(arr[1][0].GetString());
            var yC1 = new BigInteger(arr[1][1].GetString());

            var x = new Fp2(xC1, xC0);
            var y = new Fp2(yC1, yC0);
            return TwistPoint.FromAffine(x, y);
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
            var x = new BigInteger((string)arr[0]);
            var y = new BigInteger((string)arr[1]);
            return BN128Curve.Curve.CreatePoint(x, y);
        }

        private static TwistPoint ParseG2Newtonsoft(JArray arr)
        {
            var xC0 = new BigInteger((string)((JArray)arr[0])[0]);
            var xC1 = new BigInteger((string)((JArray)arr[0])[1]);
            var yC0 = new BigInteger((string)((JArray)arr[1])[0]);
            var yC1 = new BigInteger((string)((JArray)arr[1])[1]);

            var x = new Fp2(xC1, xC0);
            var y = new Fp2(yC1, yC0);
            return TwistPoint.FromAffine(x, y);
        }
#endif
    }
}
