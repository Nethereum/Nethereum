#if DEBUG

using System;
using Avalonia;

namespace NethereumGodotAvalonia;

internal static class Designer {

	public static int Main()
		=> throw new NotSupportedException("This project isn't meant to be run: it's only for Avalonia designer support.");

	// Used by designer
	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder
			.Configure<App>()
			.UseSkia();

}

#endif
