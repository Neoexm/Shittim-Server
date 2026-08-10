---
id: from-source
title: Running from source
---

The Control Center builds the server for you, so this is only worth reading if you want to change the code.

## Layout

| Folder | What is in it |
| --- | --- |
| `Shittim-Server` | the server: controllers, protocol handlers, services, commands |
| `Schale` | game models, FlatBuffers data, crypto, mapping profiles |
| `Shittim-Server.Tests` | xunit suite |
| `ShittimControlCenter` | the Electron admin app |
| `ShittimRaidConsole` | the world raid coordinator's web console |
| `Shittim-Coordinator` | the shared world raid coordinator service |
| `Scripts` | the mitmproxy redirect script |
| `Tools` | packet capture, capture verification and other one-off tooling |
| `docs` | this documentation |

## Building

```bash
dotnet build Shittim-Server/Shittim-Server.csproj
```

The server runs from its build directory and reads `Config/Config.json` relative to the executable, so run it from `Shittim-Server/`:

```bash
dotnet run --project Shittim-Server/Shittim-Server.csproj
```

`--console` attaches the in-process command console, and `--id <serverId>` picks which account console commands act on (it defaults to 2).

The running executable locks its own `bin` directory, so building while the server is up fails. Either stop it, or send the build somewhere else:

```powershell
dotnet build "Shittim-Server\Shittim-Server.csproj" "-p:OutputPath=bin\check\"
```

## Tests

```bash
dotnet test Shittim-Server.Tests/Shittim-Server.Tests.csproj
```

The suite runs every test class in parallel. If a different test fails on each run, look for process-global state rather than a broken test - `Console.SetOut` and the `SHITTIM_*` environment variables both need a collection with parallelization disabled.

## The Control Center

Vanilla ES modules, no bundler and no transpiler. `npm start` in `ShittimControlCenter` runs it from source; `npm run dist` packages it and `npm run publish` pushes a release. `npm test` runs the node test files under `test/`.

Pages are self-contained modules under `src/js/pages/` that default-export `{ id, title, icon, needsTarget, mount(root, ctx) }` and get registered in the `PAGES` array in `src/js/app.js`. `mount` returns its own teardown function. Anything that needs the main process goes through `window.host.*`, which is `preload.js` forwarding to `ipcMain.handle` in `main.js`; anything that needs the server goes through `src/js/api.js`.
