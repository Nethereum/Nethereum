using Avalonia.Controls;
using NethereumWCAvalonia.ViewModels;

namespace NethereumWCAvalonia.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            DataContext = new MainWindowViewModel();
            InitializeComponent();
           
        }
    }
}