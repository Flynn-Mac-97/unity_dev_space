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

## Layout Groups & Options

```csharp
// Foldout
showSection = EditorGUILayout.Foldout(showSection, "Section");
if (showSection)
{
    EditorGUI.indentLevel++;
    // content
    EditorGUI.indentLevel--;
}

// Spacing
GUILayout.Space(10);
GUILayout.FlexibleSpace(); // Pushes elements

// Sizing
GUILayout.Button("Wide", GUILayout.Width(200));
```

## Styling

```csharp
// Built-in styles
GUILayout.Label("Bold", EditorStyles.boldLabel);

// Custom style (cache this!)
private GUIStyle _headerStyle;
private GUIStyle HeaderStyle => _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
{
    fontSize = 16,
    alignment = TextAnchor.MiddleCenter
};
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
