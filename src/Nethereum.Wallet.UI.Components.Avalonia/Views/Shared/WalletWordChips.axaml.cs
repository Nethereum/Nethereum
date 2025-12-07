using Avalonia;
using Avalonia.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Threading.Tasks;
using Avalonia.Input;
using ReactiveUI;
using Avalonia.Controls.Primitives;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views.Shared
{
    public partial class WalletWordChips : UserControl
    {
        public static readonly StyledProperty<List<string>> WordsProperty =
            AvaloniaProperty.Register<WalletWordChips, List<string>>(nameof(Words), defaultValue: new List<string>());

        public List<string> Words
        {
            get => GetValue(WordsProperty);
            set => SetValue(WordsProperty, value);
        }

        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<WalletWordChips, string>(nameof(Title), defaultValue: "Your Seed Phrase");

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly StyledProperty<string> SubtitleProperty =
            AvaloniaProperty.Register<WalletWordChips, string>(nameof(Subtitle), defaultValue: "Write these words down in order");

        public string Subtitle
        {
            get => GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public static readonly StyledProperty<string> HiddenMessageProperty =
            AvaloniaProperty.Register<WalletWordChips, string>(nameof(HiddenMessage), defaultValue: "Click the eye icon to reveal your seed phrase");

        public string HiddenMessage
        {
            get => GetValue(HiddenMessageProperty);
            set => SetValue(HiddenMessageProperty, value);
        }

        public static readonly StyledProperty<string> EmptyMessageProperty =
            AvaloniaProperty.Register<WalletWordChips, string>(nameof(EmptyMessage), defaultValue: "No words to display");

        public string EmptyMessage
        {
            get => GetValue(EmptyMessageProperty);
            set => SetValue(EmptyMessageProperty, value);
        }

        public static readonly StyledProperty<bool> ShowHeaderProperty =
            AvaloniaProperty.Register<WalletWordChips, bool>(nameof(ShowHeader), defaultValue: true);

        public bool ShowHeader
        {
            get => GetValue(ShowHeaderProperty);
            set => SetValue(ShowHeaderProperty, value);
        }

        public static readonly StyledProperty<bool> AllowToggleVisibilityProperty =
            AvaloniaProperty.Register<WalletWordChips, bool>(nameof(AllowToggleVisibility), defaultValue: true);

        public bool AllowToggleVisibility
        {
            get => GetValue(AllowToggleVisibilityProperty);
            set => SetValue(AllowToggleVisibilityProperty, value);
        }

        public static readonly StyledProperty<bool> AllowCopyProperty =
            AvaloniaProperty.Register<WalletWordChips, bool>(nameof(AllowCopy), defaultValue: true);

        public bool AllowCopy
        {
            get => GetValue(AllowCopyProperty);
            set => SetValue(AllowCopyProperty, value);
        }

        public static readonly StyledProperty<bool> IsVisibleProperty =
            AvaloniaProperty.Register<WalletWordChips, bool>(nameof(IsVisible));

        public bool IsVisible
        {
            get => GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public static readonly StyledProperty<ICommand> IsVisibleChangedCommandProperty =
            AvaloniaProperty.Register<WalletWordChips, ICommand>(nameof(IsVisibleChangedCommand));

        public ICommand IsVisibleChangedCommand
        {
            get => GetValue(IsVisibleChangedCommandProperty);
            set => SetValue(IsVisibleChangedCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OnCopyCommandProperty =
            AvaloniaProperty.Register<WalletWordChips, ICommand>(nameof(OnCopyCommand));

        public ICommand OnCopyCommand
        {
            get => GetValue(OnCopyCommandProperty);
            set => SetValue(OnCopyCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> ToggleVisibilityCommandProperty =
            AvaloniaProperty.Register<WalletWordChips, ICommand>(nameof(ToggleVisibilityCommand));

        public ICommand ToggleVisibilityCommand
        {
            get => GetValue(ToggleVisibilityCommandProperty);
            set => SetValue(ToggleVisibilityCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> CopyToClipboardCommandProperty =
            AvaloniaProperty.Register<WalletWordChips, ICommand>(nameof(CopyToClipboardCommand));

        public ICommand CopyToClipboardCommand
        {
            get => GetValue(CopyToClipboardCommandProperty);
            set => SetValue(CopyToClipboardCommandProperty, value);
        }

        public WalletWordChips()
        {
            InitializeComponent();
            ToggleVisibilityCommand = ReactiveCommand.Create(ToggleVisibility);
            CopyToClipboardCommand = ReactiveCommand.CreateFromTask(CopyToClipboard);
        }

        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
            IsVisibleChangedCommand?.Execute(IsVisible);
        }

        public async Task CopyToClipboard()
        {
            if (Words?.Any() == true)
            {
                var phraseText = string.Join(" ", Words);
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(phraseText);
                }
                OnCopyCommand?.Execute(phraseText);
            }
        }

        public IEnumerable<(int index, string word)> GetIndexedWords()
        {
            return Words?.Select((word, i) => (i + 1, word)) ?? Enumerable.Empty<(int, string)>();
        }
    }
}
