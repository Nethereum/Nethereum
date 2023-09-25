using Avalonia;
using Avalonia.ReactiveUI;
using Godot;
using JLeb.Estragonia;

namespace NethereumGodotAvalonia;

public partial class AvaloniaLoader : Node {

	public override void _Ready()
		=> AppBuilder
			.Configure<App>()
			.UseGodot()
			.UseReactiveUI()
			.SetupWithoutStarting();

}
