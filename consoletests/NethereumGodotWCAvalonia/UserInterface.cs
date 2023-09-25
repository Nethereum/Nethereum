using Godot;
using JLeb.Estragonia;
using NethereumGodotAvalonia.Views;

namespace NethereumGodotAvalonia;

public partial class UserInterface : AvaloniaControl {

	public override void _Ready() {
		GetWindow().SetImeActive(true);

		Control = new MainWindow();

		base._Ready();
	}

	public override void _Process(double delta) {
		//((HelloWorldView) Control!).FpsCounter.Text = $"FPS: {Engine.GetFramesPerSecond():F0}";

		base._Process(delta);
	}

}
