# REA provider matrix

| Input | Provider | Observed result |
|---|---|---|
| Managed DLL | REA static .NET | Two synthetic assemblies; type, method and call evidence |
| JavaScript modules | Static application analysis | One application; import graph verified |
| Native Windows PE | Ghidra | Provider unavailable; zero native analyses |

The pinned Windows Ghidra notes describe experimental support. They require a supported Ghidra release and a matching 64-bit JDK. Version 12.1.x documents JDK 21 or later. No host installation was performed. No native performance or compatibility claim follows.
