using System.Numerics;

namespace Nethereum.WebAuthn.UnitTests
{
    public static class WebAuthnReferenceVectors
    {
        public class Vector
        {
            public bool RequireUV;
            public BigInteger PubKeyX;
            public BigInteger PubKeyY;
            public string Challenge = "";
            public BigInteger R;
            public BigInteger S;
            public string AuthenticatorDataHex = "";
            public string ClientDataJson = "";
            public int TypeIndex;
            public int ChallengeIndex;
        }

        public static readonly Vector Case0 = new Vector
        {
            RequireUV = false,
            PubKeyX = BigInteger.Parse("66296829923831658891499717579803548012279830557731564719736971029660387468805"),
            PubKeyY = BigInteger.Parse("46098569798045992993621049610647226011837333919273603402527314962291506652186"),
            Challenge = "9jEFijuhEWrM4SOW-tChJbUEHEP44VcjcJ-Bqo1fTM8",
            R = BigInteger.Parse("23510924181331275540501876269042668160690304423490805737085519687669896593880"),
            S = BigInteger.Parse("36590747517247563381084733394442750806324326036343798276847517765557371045088"),
            AuthenticatorDataHex = "0x49960de5880e8c687434170f6476605b8fe4aeb9a28632c7995cf3ba831d97630100000001",
            ClientDataJson = "{\"type\":\"webauthn.get\",\"challenge\":\"9jEFijuhEWrM4SOW-tChJbUEHEP44VcjcJ-Bqo1fTM8\",\"origin\":\"http://localhost:8080\",\"crossOrigin\":false}",
            TypeIndex = 1,
            ChallengeIndex = 23
        };

        public static readonly Vector Case1 = new Vector
        {
            RequireUV = true,
            PubKeyX = BigInteger.Parse("77427310596034628445756159459159056108500819865614675054701790516611205123311"),
            PubKeyY = BigInteger.Parse("20591151874462689689754215152304668244192265896034279288204806249532173935644"),
            Challenge = "9jEFijuhEWrM4SOW-tChJbUEHEP44VcjcJ-Bqo1fTM8",
            R = BigInteger.Parse("70190788404940879339470429048068864326256942039718306809827270917601845266065"),
            S = BigInteger.Parse("372310544955428259193186543685199264627091796694315697785543526117532572367"),
            AuthenticatorDataHex = "0x49960de5880e8c687434170f6476605b8fe4aeb9a28632c7995cf3ba831d97630500000001",
            ClientDataJson = "{\"type\":\"webauthn.get\",\"challenge\":\"9jEFijuhEWrM4SOW-tChJbUEHEP44VcjcJ-Bqo1fTM8\",\"origin\":\"http://localhost:8080\",\"crossOrigin\":false}",
            TypeIndex = 1,
            ChallengeIndex = 23
        };

        public static IEnumerable<object[]> All()
        {
            yield return new object[] { Case0 };
            yield return new object[] { Case1 };
        }
    }
}
