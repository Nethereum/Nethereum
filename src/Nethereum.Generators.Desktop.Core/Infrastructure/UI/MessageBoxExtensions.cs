using Eto.Forms;

namespace Nethereum.Generators.Desktop.Core.Infrastructure.UI
{
    public static class MessageBoxControlExtensions
    {
        public static void ShowInformation(this Control control, string message)
        {
            MessageBox.Show(control, message, Properties.Resources.TextInformation);
        }

        public static bool ShowQuestion(this Control control, string message, string title, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Yes)
        {
            return MessageBox.Show(control, message, title, MessageBoxButtons.YesNo, MessageBoxType.Question, defaultButton) == DialogResult.Yes;
        }

        public static void ShowWarning(this Control control, string message)
        {
            MessageBox.Show(control, message, Properties.Resources.TextWarning, MessageBoxType.Warning);
        }

        public static void ShowError(this Control control, string message)
        {
            MessageBox.Show(control, message, Properties.Resources.TextError, MessageBoxType.Error);
        }
    }
}
