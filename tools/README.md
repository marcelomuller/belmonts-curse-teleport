# Metadata inspection scripts

Read the BepInEx interop assemblies (`<game>/BepInEx/interop/*.dll`) to find game types and members.
IL2CPP interop DLLs only carry signatures, not method bodies.

Needs [dnfile](https://pypi.org/project/dnfile/): `pip install --target <dir> dnfile`, then run with
`PYTHONPATH=<dir>`. Each takes an assembly path and a regex.

| Script | Lists |
|---|---|
| `dump.py <dll> <type regex>` | methods (with parameter names) of matching types, plus the base type |
| `props.py <dll> <type regex>` | properties (interop fields show up as properties) of matching types |
| `owner.py <dll> <method regex>` | `Type.Method(params)` for every method whose name matches |

Other tricks used while building the mod:

- `strings -n 4 Assembly-CSharp.dll | grep NativeMethodInfoPtr_<Name>_` shows a method's visibility and
  parameter/return types, e.g. `NativeMethodInfoPtr_TryLoadLevelState_Public_Boolean_LevelID_MainGameLoadingType_Action_0`.
- To learn a member's type, compile a throwaway file that calls a missing member on it
  (`x.someProperty.__A();`); the compiler error names the type.
- Decompile another mod's DLL (not the game; its IL2CPP code has no bodies):
  `DOTNET_ROOT=~/.dotnet-sdk DOTNET_ROLL_FORWARD=Major ~/.dotnet-tools/ilspycmd -p -o <out> <dll>`
