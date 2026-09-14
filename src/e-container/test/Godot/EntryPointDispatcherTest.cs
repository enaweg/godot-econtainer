using System;
using System.Collections.Generic;
using Enaweg.Container.Godot;
using GdUnit4;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

[TestSuite]
[RequireGodotRuntime]
public class EntryPointDispatcherTest
{
    sealed class RecordingEntryPoint(List<string> log) : IInitializable, IPostInitializable, ITickable, IPhysicsTickable
    {
        public void Initialize() => log.Add("Initialize");
        public void PostInitialize() => log.Add("PostInitialize");
        public void Tick(long frameCount) => log.Add("Tick");
        public void PhysicsTick(long frameCount) => log.Add("PhysicsTick");
    }

    sealed class ThrowingInitializable : IInitializable
    {
        public void Initialize() => throw new InvalidOperationException("init failed");
    }

    static ContainerBuilder NewBuilderWithLog(out List<string> log)
    {
        var builder = new ContainerBuilder();
        log = new List<string>();
        builder.RegisterInstance(log);
        return builder;
    }

    [TestCase]
    public void Dispatch_CallsInitializeThenPostInitialize_InThatOrder()
    {
        var builder = NewBuilderWithLog(out var log);
        builder.RegisterEntryPoint<RecordingEntryPoint>();

        using var resolver = builder.Build();

        AssertArray(log).ContainsExactly("Initialize", "PostInitialize");
    }

    [TestCase]
    public void Dispatch_RegistersTickable_AndRunsOnNextProcessFrame()
    {
        var builder = NewBuilderWithLog(out var log);
        builder.RegisterEntryPoint<RecordingEntryPoint>();

        using var resolver = builder.Build();
        GodotFrameProvider.Process.Run(0.016);

        AssertArray(log).ContainsExactly("Initialize", "PostInitialize", "Tick");
    }

    [TestCase]
    public void Dispatch_RegistersPhysicsTickable_AndRunsOnNextPhysicsFrame()
    {
        var builder = NewBuilderWithLog(out var log);
        builder.RegisterEntryPoint<RecordingEntryPoint>();

        using var resolver = builder.Build();
        GodotFrameProvider.PhysicsProcess.Run(0.016);

        AssertArray(log).ContainsExactly("Initialize", "PostInitialize", "PhysicsTick");
    }

    [TestCase]
    public void Dispatch_InitializeException_WithHandler_IsPublished_AndOtherEntryPointsStillRun()
    {
        Exception? published = null;
        var builder = NewBuilderWithLog(out var log);
        builder.RegisterEntryPointExceptionHandler(ex => published = ex);
        builder.RegisterEntryPoint<ThrowingInitializable>();
        builder.RegisterEntryPoint<RecordingEntryPoint>();

        using var resolver = builder.Build();

        AssertObject(published).IsInstanceOf<InvalidOperationException>();
        AssertArray(log).Contains("Initialize");
    }

    [TestCase]
    public void Dispatch_InitializeException_WithoutHandler_DoesNotThrow()
    {
        var builder = new ContainerBuilder();
        builder.RegisterEntryPoint<ThrowingInitializable>();

        using var resolver = builder.Build();
    }
}
