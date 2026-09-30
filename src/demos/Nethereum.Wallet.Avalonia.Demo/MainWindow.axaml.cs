using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI.Components.Avalonia.Views;
using System;

namespace Nethereum.Wallet.Avalonia.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var testWindow = new TestWindow();
        testWindow.Show();

        if (App.Services != null)
        {
            Content = NethereumWallet.Create(App.Services);
        }
    }
}