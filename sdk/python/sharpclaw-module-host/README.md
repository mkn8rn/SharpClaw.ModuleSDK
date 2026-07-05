# sharpclaw-module-host

`sharpclaw-module-host` helps a Python SharpClaw module run in the normal
out-of-process module host model. A module process creates a SharpClaw host,
registers endpoints, tools, inline tools, storage contracts, or protocol
contracts, and then serves the private control address passed by SharpClaw. The
helper handles the protocol routes, token check, discovery response, lifecycle
calls, and host capability client.

A practical Python module directory contains `module.json`, the module entry
script, and this package as a dependency. The manifest points SharpClaw at the
Python entrypoint and selects the out-of-process host mode. SharpClaw then
starts the process, provides the control environment variables, and talks to the
module over the foreign-module protocol.

```python
from sharpclaw_module_host import create_sharpclaw_host

host = create_sharpclaw_host(
    module_id="sample_python_module",
    tool_prefix="samplepy",
    tools=[
        {
            "name": "echo",
            "description": "Echoes a message.",
            "handler": lambda context: context.parameters.get("message", ""),
        }
    ],
)

host.serve()
```

SharpClaw supplies the control address, control token, module directory, module
data directory, and optional host capability endpoint through environment
variables. Module code normally does not need to bind an HTTP server manually;
it describes the module surface and lets this helper handle the protocol.
