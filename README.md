# SharpClaw.Modules.Hosting

`SharpClaw.Modules.Hosting` is for host and runtime code that loads compiled
SharpClaw .NET module assemblies. Its `ModuleLoadContext` creates a collectible
assembly load context for one module entry DLL, resolves that module's private
dependencies from the module folder, and keeps host-owned contract assemblies
in the default load context so module objects can still be cast to the host's
SharpClaw contract interfaces.

Reference this package when you are writing a SharpClaw host, gateway, test
harness, or runtime adapter that needs to load a module DLL from disk. A normal
module implementation usually references `SharpClaw.Contracts` for
`ISharpClawCoreModule` and related DTOs; it only needs this package when it is
itself responsible for loading another module assembly.

The smallest host-side shape is to create one load context for the module entry
assembly, load the assembly through that context, and then find the module type
using the shared contract interfaces already loaded by the host.

```csharp
using System.Reflection;
using SharpClaw.Contracts.Modules;
using SharpClaw.Modules.Hosting;

var entryAssemblyPath = Path.Combine(moduleDirectory, "MyModule.dll");
var loadContext = new ModuleLoadContext(entryAssemblyPath);
var assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(entryAssemblyPath));

var moduleType = assembly.GetTypes()
    .Single(type => type.IsAssignableTo(typeof(ISharpClawCoreModule)) && !type.IsAbstract);
```

Use a separate `ModuleLoadContext` for each loaded module instance. When the
module is unloaded, release references to objects and types from that assembly
before calling `Unload`; the runtime can only collect the context after those
references are gone.
