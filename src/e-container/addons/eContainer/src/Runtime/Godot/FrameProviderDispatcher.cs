using System.Runtime.CompilerServices;

namespace Enaweg.Container.Godot;

public partial class FrameProviderDispatcher : global::Godot.Node
{
	StrongBox<double> processDelta = new StrongBox<double>();
	StrongBox<double> physicsProcessDelta = new StrongBox<double>();

	public override void _Ready()
	{
		GodotFrameProvider.Process.Delta = processDelta;
		GodotFrameProvider.PhysicsProcess.Delta = physicsProcessDelta;
	}

	public override void _Process(double delta)
	{
		processDelta.Value = delta;
		GodotTimeProvider.Process.time += delta;
		GodotFrameProvider.Process.Run(delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		physicsProcessDelta.Value = delta;
		GodotTimeProvider.PhysicsProcess.time += delta;
		GodotFrameProvider.PhysicsProcess.Run(delta);
	}
}
