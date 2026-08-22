<div align="center">

# eContainer

**[VContainer](https://github.com/hadashiA/VContainer) - Unity's fast DI container - ported to [Godot](https://godotengine.org/).**

![CI](https://github.com/enaweg/godot-econtainer/actions/workflows/ci-release.yml/badge.svg)
![Godot 4.7.2](https://img.shields.io/badge/Godot-v4.7.2-202020?logo=godot-engine&logoColor=blue&color=darkgreen&labelColor=202020)
![Dotnet 8](https://img.shields.io/badge/8-02020?logo=dotnet&logoSize=auto&logoColor=purple&color=darkgreen&labelColor=E0E0E0)

**NOTE**: This project is experimental and still a work in progress.

</div>

## Requirements

The current build configuration uses:

+ [Godot 4.7.2 .NET](https://godotengine.org/download/archive/4.7.2-stable/)
+ [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

The project targets `net8.0`. Android builds target `net9.0`.

## Installation

1. Download the latest [eContainer release](https://github.com/enaweg/godot-econtainer/releases) and
   [ePlugin release](https://github.com/enaweg/godot-epluginframework/releases).
2. Extract `addons/eContainer` from the eContainer archive and `addons/ePlugin` from the ePlugin archive into your
   Godot project's `addons` directory.
3. Open the project in the Godot .NET editor and enable **ePlugin** under **Project > Project Settings > Plugins**.
4. Enable **eContainer**.

eContainer depends on ePlugin. When it is enabled, ePlugin adds the bundled
`VContainer.Standalone` and `VContainer.SourceGenerator` packages to the Godot project, registers their local
package source in `nuget.config`, and adds the `eContainer` autoload. No manual `dotnet add package` step is needed.

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
        builder.Register<IPlayerService, PlayerService>(Lifetime.Singleton);
        builder.Register<EnemySpawner>(Lifetime.Scoped);
    }
}
```

Attach `GameLifetimeScope` to a node in a scene, or create one from code with `LifetimeScope.Create(...)`.
`LifetimeScope` resolves its parent automatically - normally the `RootLifetimeScope` autoload - so registrations
cascade down the scope hierarchy.

Resolved classes can opt into the entry-point lifecycle by implementing the annotation interfaces:

```csharp
using Enaweg.Container.Annotations;

public class PlayerService : IInitializable, ITickable
{
    public void Initialize() { /* runs once the scope resolves */ }
    public void Tick() { /* runs every _Process */ }
}
```

## Development

To build the C# assembly:

```bash
dotnet build src/e-container/gContainer.csproj
```

Opening `src/e-container/project.godot` in the Godot 4.7.2 .NET editor also triggers a build automatically and is the
normal way to exercise the plugins. There is currently no automated test project in this repository.

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
