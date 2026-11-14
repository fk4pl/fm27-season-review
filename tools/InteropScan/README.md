# InteropScan

Developer tool. It is not part of the mod and is never included in the release package.

FM27 is an IL2CPP game: engine code the game does not use is stripped from the build, and
calling a stripped method from a plugin throws `NotSupportedException: Method unstripping failed`
at run time. InteropScan reads the interop assemblies that BepInEx generates on the first start
(`<game>/BepInEx/interop/`) and reports, without starting the game, which methods can be called.

It only reads those files. Nothing from the game folder is copied into this repository.

## Usage

```
dotnet run --project tools/InteropScan -- "<game>/BepInEx/interop" <list|bad|sum|type> <assembly.dll> <type-regex> [method-regex]
```

| Mode | Output |
|---|---|
| `list` | every matching method with its status |
| `bad` | only methods that will throw (`FAILED`, `BROKEN`) |
| `sum` | one line per type with counts |
| `type` | how the type was generated: value type or class, and its fields |

| Status | Meaning |
|---|---|
| `NATIVE` | calls the method compiled into the game; safe |
| `ICALL` | calls an engine-internal function; normally safe |
| `MANAGED` | re-created from Unity's reference assemblies and calls nothing that is stripped |
| `FAILED` | stripped and not re-created; throws when called |
| `BROKEN via ...` | re-created, but reaches a `FAILED` method through the calls shown |

Examples:

```
# Which IMGUI calls survive? (GUI.Label and GUI.Box do; GUI.DrawTexture and all of GUILayout do not.)
dotnet run --project tools/InteropScan -- "$GAME/BepInEx/interop" list UnityEngine.IMGUIModule.dll '^UnityEngine\.(GUI|GUILayout)$' '^(Label|Box|DrawTexture)$'

# Is everything the panel uses from UI Toolkit still there after a game update?
dotnet run --project tools/InteropScan -- "$GAME/BepInEx/interop" bad UnityEngine.UIElementsModule.dll '^UnityEngine\.UIElements\.(UIDocument|PanelSettings|VisualElement|Label|ScrollView|Button|IStyle)$'
```

## Limits

- The result is a prediction from the generated code. For the IMGUI calls tried in the game
  (12 methods) it matched exactly, but it is not a substitute for testing in the game.
- It cannot tell whether a generic method or generic type works for a particular type argument
  (for example `StyleEnum<FlexDirection>` or `RegisterCallback<ClickEvent>`).
- It says nothing about behaviour: drawing, input routing, themes and fonts must be checked in
  the game.
- Run it again after every game update: the interop assemblies are regenerated and the set of
  stripped methods can change.
