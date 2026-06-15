# IMGUI Elements and Layout

## Common GUI Elements

| Method | Use Case |
|--------|----------|
| `GUILayout.Label()` | Text display |
| `GUILayout.Button()` | Clickable button |
| `GUILayout.TextField()` | Text input |
| `GUILayout.Toggle()` | Checkbox |
| `GUILayout.Slider()` | Value slider |

## Editor-Specific Elements

| Method | Use Case |
|--------|----------|
| `EditorGUILayout.PropertyField()` | Serialized property (auto) |
| `EditorGUILayout.ObjectField()` | Object reference |
| `EditorGUILayout.EnumPopup()` | Enum dropdown |
| `EditorGUILayout.Foldout()` | Collapsible section |
| `EditorGUILayout.HelpBox()` | Info/warning/error box |

## Layout Groups

```csharp
// Horizontal
GUILayout.BeginHorizontal();
// content
GUILayout.EndHorizontal();

// Vertical
GUILayout.BeginVertical("box");
// content
GUILayout.EndVertical();

// Scroll View
scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
// content
EditorGUILayout.EndScrollView();
```

## Change Check

```csharp
EditorGUI.BeginChangeCheck();
value = EditorGUILayout.IntField("Value", value);
if (EditorGUI.EndChangeCheck())
{
    // Do something on change
}
```
