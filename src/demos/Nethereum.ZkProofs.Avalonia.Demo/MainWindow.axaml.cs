using Avalonia.Controls;

namespace Nethereum.ZkProofs.Avalonia.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ZkProofViewModel vm)
            {
                await vm.InitializeAsync();
            }
        };
    }
}
