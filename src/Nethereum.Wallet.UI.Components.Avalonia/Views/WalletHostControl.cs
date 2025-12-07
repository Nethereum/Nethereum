using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Nethereum.Wallet.UI.Components.Avalonia.ViewModels;

namespace Nethereum.Wallet.UI.Components.Avalonia.Views
{
    public class WalletHostControl : UserControl
    {
        private readonly WalletHostViewModel _viewModel;

        public WalletHostControl(WalletHostViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = viewModel;
            BuildContent();
            _ = viewModel.InitializeAsync();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _viewModel.Dispose();
        }

        private void BuildContent()
        {
            var rootGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
                Margin = new Thickness(16)
            };

            var statusPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 8
            };

            statusPanel.Children.Add(new TextBlock
            {
                Text = "Nethereum Wallet",
                FontSize = 20,
                FontWeight = FontWeight.Bold
            });

            statusPanel.Children.Add(new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding("Wallet.IsWalletUnlocked")
                {
                    StringFormat = "Wallet unlocked: {0}"
                }
            });

            statusPanel.Children.Add(new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding("Wallet.HasAccounts")
                {
                    StringFormat = "Accounts detected: {0}"
                }
            });

            rootGrid.Children.Add(statusPanel);

            var overlayBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                [!IsVisibleProperty] = new Binding("IsOverlayVisible")
            };

            var overlayStack = new StackPanel
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 30, 30, 30)),
                Spacing = 12,
                Margin = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            overlayStack.Children.Add(new TextBlock
            {
                Text = "Pending request",
                FontSize = 18,
                FontWeight = FontWeight.SemiBold
            });

            overlayStack.Children.Add(new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding("CurrentPrompt.DAppName")
                {
                    StringFormat = "DApp: {0}"
                }
            });

            overlayStack.Children.Add(new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding("CurrentPrompt.Type")
                {
                    StringFormat = "Type: {0}"
                }
            });

            overlayStack.Children.Add(new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding("CurrentPrompt.Origin")
                {
                    StringFormat = "Origin: {0}"
                }
            });

            var dismissButton = new Button
            {
                Content = "Dismiss",
                Width = 120,
                HorizontalAlignment = HorizontalAlignment.Center,
                [!Button.CommandProperty] = new Binding("DismissPromptCommand")
            };

            overlayStack.Children.Add(dismissButton);
            overlayBorder.Child = overlayStack;

            rootGrid.Children.Add(overlayBorder);

            Content = rootGrid;
        }
    }
}
