using System;
using System.Text;
using Nethereum.Wallet;
using Xunit;

public class KeyStoreEncryptionStrategyTests
{
    [Fact]
    public void EncryptDecryptRoundTripUsingKeystoreStrategy()
    {
        var strategy = new KeyStoreEncryptionStrategy();
        var password = "very-secure-password";
        var payload = Encoding.UTF8.GetBytes("{\"data\":\"test\"}");

        var encrypted = strategy.Encrypt(payload, password);
        Assert.NotEqual(payload, encrypted);

        var decrypted = strategy.Decrypt(encrypted, password);
        Assert.Equal(payload, decrypted);
    }

    [Fact]
    public void DecryptWithWrongPasswordThrows()
    {
        var strategy = new KeyStoreEncryptionStrategy();
        var payload = Encoding.UTF8.GetBytes("payload");
        var encrypted = strategy.Encrypt(payload, "correct");

        Assert.ThrowsAny<Exception>(() => strategy.Decrypt(encrypted, "wrong"));
    }
}
