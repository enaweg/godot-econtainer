# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

`godot-econtainer` is a port of Unity's [VContainer](https://github.com/hadashiA/VContainer) dependency-injection
library to the Godot Engine (4.7, C#/.NET). The actual Godot project lives under `src/e-container/` (Godot project
root, assembly name `gContainer`), and the DI library ships as a Godot editor addon at
`src/e-container/addons/eContainer/`.

A second addon, `src/e-container/addons/ePlugin/`, is a small in-house framework for building declarative,
self-installing Godot C# editor plugins. `eContainer` is built on top of `ePlugin` and depends on it.

**`addons/ePlugin/` is read-only.** It is vendored here like an external/NuGet dependency — this repo simply
tracks its latest released version. Never modify, refactor, add tests for, or otherwise change any file under
`addons/ePlugin/` as part of work on this repo. If a change to `ePlugin` itself seems necessary, say so instead of
editing it here.

## Commands

There is no separate CLI build step outside of Godot/.NET tooling — the addons are Godot editor plugins, not
standalone libraries.

- Build the C# assembly: `dotnet build src/e-container/gContainer.csproj` (targets `net8.0`, or `net9.0` when
  `GodotTargetPlatform=android`).
- Opening `src/e-container/project.godot` in the Godot 4.7 editor will trigger a dotnet build automatically and is
  the normal way to exercise the plugins (they register/unregister themselves via the editor plugin lifecycle).
- There is currently no automated test project in the repo (no `*Test*.csproj`). `IDotnetCli.RunTests()` exists as
  a hook in the `ePlugin` CLI abstraction but has no implementation/target wired up yet.
- CI (`.github/workflows/ci-release.yml`) only runs on `v*` tag pushes: it stamps the version into
  `addons/eContainer/plugin.cfg`, zips `addons/eContainer` into a release artifact, and drafts a GitHub release.
  There is no build/lint/test CI on pushes or PRs.

## Architecture

### `ePlugin` — declarative editor plugin framework (`addons/ePlugin/`, read-only)

Treat this directory as vendored/external code (see note above) — described here only so its behavior can be
understood, not modified. Editor-only code (`#if TOOLS`) that lets a Godot C# `EditorPlugin` describe what it needs via a fluent recipe
instead of imperatively wiring things up in `_EnablePlugin`/`_DisablePlugin`:

- `IEEditorPlugin.CreateRecipe(IEEditorPluginBuilder)` — plugins implement this to declare autoloads, plugin
  dependencies (with optional version constraints), NuGet packages, extra `.csproj` project references, and
  directories whose editor/IDE visibility should track the plugin's enabled state.
- `EGlobal` (singleton) drives the actual install/uninstall of a plugin's recipe, resolves plugin dependency
  ordering (enabling dependencies first, disabling dependents first), and tracks each plugin's state
  (`Created` → `Activated`/`Deactivated`/`Error`) in a `PluginContext`.
- `EGlobal` re-initializes itself from `EPluginPlugin._Process` because a C# assembly reload in the editor wipes
  static state; `ReloadContexts` rebuilds `PluginContext`s from the live scene tree of editor plugin nodes.
- Actual `dotnet` CLI work (adding/removing NuGet packages, solution/project references) is abstracted behind
  `IDotnetCli`, implemented differently per SDK version (`DotnetCli9` vs `DotnetCli10`, selected by
  `DotnetVersionManager` based on the installed `dotnet --version`).
- `EPlugin` is the public static entry point other code uses to reach the running framework instance (e.g. to call
  `EPlugin.RegisterInitializer`) without doing the scene-tree lookup itself.

### `eContainer` — the VContainer port (`addons/eContainer/`)

- `EContainerPlugin` (editor-only) declares its `ePlugin` recipe: install the vendored `VContainer.Standalone` /
  `VContainer.SourceGenerator` NuGet packages from `.libs/` (local `.nupkg` files, not a public feed) and register
  the `eContainer.tscn` autoload.
- Runtime code lives in `src/Runtime/` and is split into:
  - `Godot/` — the Godot-specific adaptation layer of VContainer, most importantly `LifetimeScope`
    (`src/Runtime/Godot/LifetimeScope.cs`), the DI scope node ported from VContainer's Unity `MonoBehaviour` scope
    to a Godot `Node`. It builds/tears down its `IObjectResolver` container on `_EnterTree`/`_ExitTree` instead of
    `Awake`/`OnDestroy`, resolves its parent scope via `ParentReference` (explicit object, discovered type name, a
    `FindParent()` override, or a global override stack), and supports child scopes created either in code
    (`CreateChild`) or instantiated from a `PackedScene` (`CreateChildFromPackedScene`).
  - `RootLifetimeScope` — the single top-level scope (registered as the `eContainer` autoload), which also handles
    scopes whose declared parent type hasn't entered the tree yet via a `WaitingList` that gets flushed as matching
    parents become ready or the current scene changes.
  - `EntryPointsBuilder` / `EntryPointDispatcher` / `FrameProviderDispatcher` / `GodotFrameProvider` /
    `GodotTimeProvider` — wire VContainer's entry-point/tickable system to Godot's `_Process`/`_PhysicsProcess`
    callbacks.
  - `Annotations/` — marker interfaces resolved objects implement to participate in the lifecycle:
    `IInitializable`, `IPostInitializable`, `ITickable`, `IPhysicsTickable`.
  - `Internal/` — supporting data structures shared with upstream VContainer (`FreeListCore`,
    `CompositeDisposable`, `FuncRegistrationBuilder`, etc.).
  - `Editor/` — Godot inspector customizations for `LifetimeScope`'s parent reference field
    (`ParentReferenceEditorProperty`, `LifeScopeInspectorPlugin`).

### Plugin dependency direction

`eContainer` depends on `ePlugin` (declared via `AddPluginDependency("ePlugin")` in `EEditorPluginBuilder`'s
constructor) — `ePlugin` has no knowledge of `eContainer` and is meant to be reusable by other Enaweg editor
plugins.
