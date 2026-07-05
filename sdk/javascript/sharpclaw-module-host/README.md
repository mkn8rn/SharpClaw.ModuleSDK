# @sharpclaw/module-host

`@sharpclaw/module-host` helps a JavaScript SharpClaw module run in the normal
out-of-process module host model. A module process creates a SharpClaw host,
registers endpoints, tools, inline tools, storage contracts, or protocol
contracts, and then listens on the private control address passed by SharpClaw.
The helper handles the protocol routes, token check, discovery response,
lifecycle calls, and host capability client.

A practical JavaScript module directory contains `module.json`, the module
entry script, and this package as a dependency. The manifest points SharpClaw at
the JavaScript entrypoint and selects the out-of-process host mode. SharpClaw
then starts the process, provides the control environment variables, and talks
to the module over the foreign-module protocol.

```js
import { createSharpClawHost } from '@sharpclaw/module-host';

const host = createSharpClawHost({
  moduleId: 'sample_js_module',
  toolPrefix: 'samplejs',
  tools: [
    {
      name: 'echo',
      description: 'Echoes a message.',
      handler: context => context.parameters.message ?? ''
    }
  ]
});

await host.start();
```

SharpClaw supplies the control address, control token, module directory, module
data directory, and optional host capability endpoint through environment
variables. Module code normally does not need to bind an HTTP server manually;
it describes the module surface and lets this helper handle the protocol.
