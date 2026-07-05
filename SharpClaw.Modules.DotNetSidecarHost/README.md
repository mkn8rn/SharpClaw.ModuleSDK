# SharpClaw.Modules.DotNetSidecarHost

`SharpClaw.Modules.DotNetSidecarHost` runs a compiled SharpClaw .NET module in
the sidecar process model. The host process starts this executable with the
module directory and control endpoint in environment variables, then talks to it
over the SharpClaw foreign-module protocol. The sidecar host loads the module
entry assembly through `ModuleLoadContext`, validates the module identity from
`module.json`, exposes lifecycle and tool endpoints, and proxies host
capabilities such as task execution, provider calls, module storage, and agent
operations back to the parent SharpClaw runtime.

Reference this package from SharpClaw runtime or packaging code that needs to
ship the .NET sidecar executable next to module-loading infrastructure. A normal
module implementation does not reference this package directly; module projects
normally reference `SharpClaw.Contracts` for `ISharpClawCoreModule` and related
DTOs. Use the sidecar host package when you are assembling the runtime payload
that launches .NET modules out of process.

At runtime the parent host provides the environment values identified by
`ForeignModuleProtocol.ModuleDirectoryEnv`,
`ForeignModuleProtocol.ControlAddressEnv`, and
`ForeignModuleProtocol.ControlTokenEnv`. The module directory must contain
`module.json` and the entry assembly named by that manifest. A
sidecar-compatible .NET module manifest sets `runtime.name` to `dotnet` and
`runtime.hostMode` to `sidecar`, then points `entryAssembly` at the module DLL.

```json
{
  "id": "sample_module",
  "displayName": "Sample Module",
  "version": "1.0.0",
  "toolPrefix": "sample",
  "entryAssembly": "Sample.Module.dll",
  "runtime": {
    "name": "dotnet",
    "hostMode": "sidecar"
  }
}
```

The sidecar executable is not a general CLI surface. It is launched by
SharpClaw with a private control token, loads exactly one module directory, and
stops when the host sends the shutdown protocol request.
