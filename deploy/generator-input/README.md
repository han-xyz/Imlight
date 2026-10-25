# Generator input

Imcodec generates its C# types from your game client's data at build time. `deploy/Dockerfile` copies the files in this folder into Imcodec before building, and refuses to build when either kind is missing. Everything here except this file is gitignored; never commit it.

| File | Goes to | How to produce it |
| --- | --- | --- |
| `*.json` | `submodule/Imcodec/src/Imcodec.ObjectProperty/GeneratorInput/` | Type dump of the running client from [WizType](https://github.com/wizspoil/wiztype). Any file name. |
| `*Messages*.xml` | `submodule/Imcodec/src/Imcodec.MessageLayer/GeneratorInput/` | Unpack `Root.wad` from the client: `dotnet run --project submodule/Imcodec/src/Imcodec.Cli wad unpack Root.wad`, then take every `*Messages*.xml`. |

Build from the repository root: `docker build -f deploy/Dockerfile -t imlight .`
