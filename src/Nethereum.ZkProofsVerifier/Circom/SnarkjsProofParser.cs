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
    public static class SnarkjsProofParser
    {
        public static Groth16Proof Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("Proof JSON is null or empty");

#if NET6_0_OR_GREATER
            return ParseWithStj(json);
#else
            return ParseWithNewtonsoft(json);
#endif
        }

#if NET6_0_OR_GREATER
        private static Groth16Proof ParseWithStj(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var piA = root.GetProperty("pi_a");
            var piB = root.GetProperty("pi_b");
            var piC = root.GetProperty("pi_c");

            var a = ParseG1Stj(piA);
            var b = ParseG2Stj(piB);
            var c = ParseG1Stj(piC);

            return new Groth16Proof { A = a, B = b, C = c };
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
        private static Groth16Proof ParseWithNewtonsoft(string json)
        {
            var obj = JObject.Parse(json);

            var piA = (JArray)obj["pi_a"];
            var piB = (JArray)obj["pi_b"];
            var piC = (JArray)obj["pi_c"];

            var a = ParseG1Newtonsoft(piA);
            var b = ParseG2Newtonsoft(piB);
            var c = ParseG1Newtonsoft(piC);

            return new Groth16Proof { A = a, B = b, C = c };
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
