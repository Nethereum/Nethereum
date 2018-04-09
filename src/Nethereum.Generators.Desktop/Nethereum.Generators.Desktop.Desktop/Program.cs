using System;
using Eto.Forms;

namespace Nethereum.Generators.Desktop.UI
{
	class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			new Application(Eto.Platform.Detect).Run(new MainForm());
		}
	}
}