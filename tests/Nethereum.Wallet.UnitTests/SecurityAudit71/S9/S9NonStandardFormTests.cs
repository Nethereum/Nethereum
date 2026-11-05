using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Nethereum.Accounts.Bip32;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Wallet.UnitTests.SecurityAudit71.S9
{
    public class S9NonStandardFormTests
    {
        private const string Abandon =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        public static readonly object[][] Shapes =
        {
            new object[] { "all uppercase", Abandon.ToUpperInvariant() },
            new object[] { "capitalised word", "Abandon " + Abandon.Substring(8) },
            new object[] { "double spaces", Abandon.Replace(" ", "  ") },
            new object[] { "trailing newline", Abandon + "\n" },
            new object[] { "tabs", Abandon.Replace(" ", "\t") },
            new object[] { "leading space", " " + Abandon },
            new object[] { "TRUE word", "TRUE abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon FALSE" }
        };

        public static System.Collections.Generic.IEnumerable<object[]> ShapeData() => Shapes;

        [Trait("Rule", "S9-R4-C")]
        [Theory]
        [MemberData(nameof(ShapeData))]
        public void Given_a_stored_phrase_whose_7_0_input_differs_from_the_canonical_form_When_checking_Then_it_is_flagged_and_the_lenient_seed_is_the_7_0_seed(string name, string phrase)
        {
            Assert.True(Bip39.IsNonStandardMnemonicForm(phrase), name);
            var salt = Encoding.UTF8.GetBytes("mnemonic");
            using var derive = new Rfc2898DeriveBytes(phrase.Normalize(NormalizationForm.FormKD), salt, 2048, HashAlgorithmName.SHA512);
            Assert.Equal(derive.GetBytes(64).ToHex(), new MinimalHDWallet(phrase, "", false).Seed.ToHex());
        }

        [Trait("Rule", "S9-R4-C")]
        [Fact]
        public void Given_the_canonical_form_When_checking_Then_it_is_never_flagged()
        {
            Assert.False(Bip39.IsNonStandardMnemonicForm(Abandon));
            Assert.False(Bip39.IsNonStandardMnemonicForm(Bip39.CanonicalMnemonic(Abandon.ToUpperInvariant())));
            for (var i = 0; i < 200; i++)
                Assert.False(Bip39.IsNonStandardMnemonicForm(Bip39.GenerateMnemonic(i % 2 == 0 ? 12 : 24)));
            Assert.False(Bip39.IsNonStandardMnemonicForm(null));
            Assert.False(Bip39.IsNonStandardMnemonicForm(""));
        }
    }
}
