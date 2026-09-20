<div align="center">

# eContainer

**[VContainer](https://github.com/hadashiA/VContainer) - Unity's fast DI container - ported to [Godot](https://godotengine.org/).**

[![CI](https://github.com/enaweg/godot-econtainer/actions/workflows/ci-pr.yml/badge.svg)](https://github.com/enaweg/godot-econtainer/actions/workflows/ci-pr.yml)
![Godot 4.7.2](https://img.shields.io/badge/Godot-v4.7.2-202020?logo=godot-engine&logoColor=blue&color=darkgreen&labelColor=202020)
![Dotnet 8](https://img.shields.io/badge/8-02020?logo=dotnet&logoSize=auto&logoColor=purple&color=darkgreen&labelColor=E0E0E0)
![VContainer 1.19.0](https://img.shields.io/badge/VContainer-v1.19.0-202020?color=darkgreen&labelColor=202020)

**NOTE**: This project is experimental and still a work in progress.

**NOTE2**: This is based on https://github.com/nazgull30

</div>

## Requirements

The current CI-tested configuration uses:

+ [Godot 4.7.2 .NET](https://godotengine.org/download/archive/4.7.2-stable/)
+ [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

The project targets `net8.0`. Android builds target `net9.0`.

eContainer bundles **[VContainer](https://github.com/hadashiA/VContainer) 1.19.0** - the `VContainer.Standalone` and
`VContainer.SourceGenerator` packages at that version ship inside the release archive under
`addons/eContainer/.libs` and are installed into your project automatically. VContainer is MIT licensed by
[hadashiA](https://github.com/hadashiA).

## Installation

1. Download the latest [eContainer release](https://github.com/enaweg/godot-econtainer/releases) and
   [ePlugin release](https://github.com/enaweg/godot-epluginframework/releases).
2. Extract `addons/eContainer` from the eContainer archive and `addons/ePlugin` from the ePlugin archive into your
   Godot project's `addons` directory.
3. Open the project in the Godot .NET editor and enable **ePlugin** under **Project > Project Settings > Plugins**.
4. Enable **eContainer**.

eContainer depends on ePlugin. When it is enabled, ePlugin adds the bundled VContainer 1.19.0 packages to the Godot
project, registers their local package source in `nuget.config`, adds the `eContainer` autoload, and un-hides the
plugin's runtime source directory. No manual `dotnet add package` step is needed.

The release archive ships that source directory hidden as `addons/eContainer/.src`, so a freshly extracted, disabled
plugin does not get compiled into your project. Enabling the plugin renames it to `src`; disabling it hides the
directory again.

## Features

+ VContainer's `IObjectResolver`/`IContainerBuilder` API adapted to Godot's `Node` lifecycle.
+ `LifetimeScope` builds and tears down a scoped container on `_EnterTree`/`_ExitTree`.
+ Child scopes resolve their parent automatically and can be created in code or from a `PackedScene`.
+ A top-level `RootLifetimeScope` autoload queues child scopes until their declared parent is ready.
+ Entry-point and tickable annotations: `IInitializable`, `IPostInitializable`, `ITickable`, and `IPhysicsTickable`.
+ Plugin installation and dependency management through the [ePlugin Framework](https://github.com/enaweg/godot-epluginframework).

## Motivation

Godot does not ship with a dependency-injection container, and maintaining scoped, hierarchical dependencies by hand
gets unwieldy in larger projects. VContainer already solves this problem for Unity, so eContainer ports its design to
Godot's `Node` and scene-tree model.

## Usage

Define a scope by extending `LifetimeScope` and registering dependencies in `Configure`:

```csharp
using Enaweg.Container.Godot;
using VContainer;

namespace YourGame;

public partial class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Entry points are the registrations that receive the lifecycle callbacks below.
        builder.RegisterEntryPoint<PlayerService>(Lifetime.Singleton);
        builder.Register<EnemySpawner>(Lifetime.Scoped);
    }
}
```

Attach `GameLifetimeScope` to a node in a scene, or create one from code with `LifetimeScope.Create(...)`.
`LifetimeScope` resolves its parent automatically - normally the `RootLifetimeScope` autoload - so registrations
cascade down the scope hierarchy.

Resolved classes can opt into the entry-point lifecycle by implementing the annotation interfaces:

```csharp
using Enaweg.Container.Godot;

public class PlayerService : IInitializable, ITickable
{
    public void Initialize() { /* runs once the scope resolves */ }
    public void Tick(long frameCount) { /* runs every _Process */ }
}
```

The lifecycle interfaces are opt-in: register their implementation with `RegisterEntryPoint`, or add it through
`UseEntryPoints`, rather than using `Register` alone. `IInitializable` runs first, followed by
`IPostInitializable`; `ITickable` and `IPhysicsTickable` then run from `_Process` and `_PhysicsProcess`, respectively.

## Development

To build the C# assembly:

```bash
dotnet nuget add source "$(pwd)/src/e-container/addons/eContainer/.libs" --name eContainer-local
dotnet build src/e-container/gContainer.sln --configuration Debug
```

The NuGet source setup is required on a clean checkout because the VContainer 1.19.0 packages are bundled in the
repository under `src/e-container/addons/eContainer/.libs` rather than published to nuget.org.

Opening `src/e-container/project.godot` in the Godot 4.7.2 .NET editor also triggers a build automatically and is the
normal way to exercise the plugins.

### Tests

Tests live in `src/e-container/test/` and run on [gdUnit4](https://github.com/MikeSchulze/gdUnit4) through the .NET
test adapter, so they need a Godot binary:

```bash
export GODOT_BIN=/path/to/godot
"$GODOT_BIN" --path src/e-container --editor --headless --quit-after 2000
dotnet test src/e-container/gContainer.sln --configuration Debug --settings src/e-container/.runsettings
```

The editor cache refresh is not optional: without it the `eContainer` autoload is never instantiated and the
`LifetimeScope` tests fail with null-reference errors.

### Release

Both pull requests and `v*` tag pushes build the solution, refresh the headless Godot editor cache, and run the full
gdUnit4 test suite. A tag push additionally stamps the tag's version into `addons/eContainer/plugin.cfg`, hides the
runtime sources as `.src`, zips `addons/eContainer` (including the bundled VContainer packages in `.libs`) into
`eContainer-v<version>.zip`, and drafts the GitHub release with a generated changelog.

### Project layout

`src/e-container/addons/` contains two plugins that are always co-deployed:

- **`ePlugin/`** - the vendored plugin lifecycle framework (upstream: [godot-epluginframework](https://github.com/enaweg/godot-epluginframework)).
- **`eContainer/`** - the VContainer port described above. It depends on ePlugin.

See `CLAUDE.md` for a deeper architecture walkthrough.

## Contribute

Feel free to contribute with documentation, testing, or pull requests.

## Commercial Support

Commercial services are available from [Enaweg](https://www.enaweg.at). If you need consulting, implementation
assistance, or tailored development services, please get in touch through their website.

## License

Licensed under the [MIT license](LICENSE).
