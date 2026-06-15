# IMGUI Templates

## EditorWindow

```csharp
using UnityEditor;
using UnityEngine;

public class MyToolWindow : EditorWindow
{
    [MenuItem("Tools/My Tool")]
    public static void ShowWindow()
    {
        GetWindow<MyToolWindow>("My Tool");
    }

    private void OnGUI()
    {
        GUILayout.Label("My Tool", EditorStyles.boldLabel);
        // Your GUI here
    }
}
```

## Custom Inspector (Undo Support)

```csharp
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MyComponent))]
public class MyComponentEditor : Editor
{
    private SerializedProperty myProperty;

    private void OnEnable()
    {
        myProperty = serializedObject.FindProperty("myField");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(myProperty);
        if (GUILayout.Button("Action")) { /* ... */ }
        serializedObject.ApplyModifiedProperties();
    }
}
```

## PropertyDrawer

```csharp
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(MyData))]
public class MyDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position = EditorGUI.PrefixLabel(position, label);
        var fieldProp = property.FindPropertyRelative("field");
        EditorGUI.PropertyField(position, fieldProp, GUIContent.none);
        EditorGUI.EndProperty();
    }
}
```

## Debug Overlay (Runtime)

```csharp
using UnityEngine;

public class DebugOverlay : MonoBehaviour
{
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 200, 100));
        GUILayout.Label($"FPS: {1f / Time.deltaTime:F1}");
        GUILayout.EndArea();
    }
}
```
