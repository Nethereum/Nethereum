using System.Threading.Tasks;

namespace Nethereum.Signer.Trezor.Abstractions
{
    /// <summary>
    /// Provides callbacks for user interactions requested by the device.
    /// </summary>
    public interface ITrezorPromptHandler
    {
        Task<string> GetPinAsync();
        Task<string> GetPassphraseAsync();
        Task ButtonAckAsync(string context);
    }

    /// <summary>
    /// Simple console implementation for CLI scenarios.
    /// </summary>
    public class ConsolePromptHandler : ITrezorPromptHandler
    {
        public Task ButtonAckAsync(string context)
        {
            System.Console.WriteLine($"Confirm on device: {context}");
            return Task.CompletedTask;
        }

        public Task<string> GetPassphraseAsync()
        {
            System.Console.Write("Enter passphrase: ");
            return Task.FromResult(System.Console.ReadLine()?.Trim() ?? string.Empty);
        }

        public Task<string> GetPinAsync()
        {
            System.Console.WriteLine("Enter PIN using the Trezor keypad mapping (device rows map to keyboard rows: 7 8 9 / 4 5 6 / 1 2 3).");
            System.Console.Write("PIN: ");
            return Task.FromResult(System.Console.ReadLine()?.Trim() ?? string.Empty);
        }
    }
}
