using Avalonia.Controls;
using NethereumGodotAvalonia.ViewModels;

namespace NethereumGodotAvalonia.Views
{
	public partial class MainWindow : UserControl
	{
		public MainWindow()
		{
			DataContext = new MainWindowViewModel();
			InitializeComponent();

		}
	}
}
