using System.Text;
using UnityEditor;
using UnityEngine;

public class NpcPromptPreviewWindow : EditorWindow
{
    private NpcPromptTemplate _template;
    private NpcPersonalityProfile _profile;

    private string _memorySummary =
        "- Player helped repair a wind pump yesterday.\n" +
        "- NPC trusts the player with settlement logistics.";

    private Vector2 _scroll;

    public static void Open(NpcPromptTemplate template)
    {
        NpcPromptPreviewWindow window = GetWindow<NpcPromptPreviewWindow>("NPC Prompt Preview");
        window._template = template;
        window.minSize = new Vector2(620f, 500f);
        window.Show();
    }

    public static void Open(NpcPromptTemplate template, NpcPersonalityProfile profile)
    {
        NpcPromptPreviewWindow window = GetWindow<NpcPromptPreviewWindow>("NPC Prompt Preview");
        window._template = template;
        window._profile = profile;
        window.minSize = new Vector2(620f, 500f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("NPC Prompt Preview", EditorStyles.boldLabel);
        _template = (NpcPromptTemplate)EditorGUILayout.ObjectField("Template", _template, typeof(NpcPromptTemplate), false);
        _profile = (NpcPersonalityProfile)EditorGUILayout.ObjectField("Personality Profile", _profile, typeof(NpcPersonalityProfile), false);

        if (_template == null)
        {
            EditorGUILayout.HelpBox("Assign an NpcPromptTemplate to preview assembled output.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        DrawInputs();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Assembled Prompt", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.SelectableLabel(BuildPreview(), EditorStyles.textArea, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Copy Assembled Prompt"))
        {
            EditorGUIUtility.systemCopyBuffer = BuildPreview();
        }
    }

    private void DrawInputs()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Memory Summary", EditorStyles.boldLabel);
        _memorySummary = EditorGUILayout.TextArea(_memorySummary, GUILayout.MinHeight(100f));
        EditorGUILayout.EndVertical();
    }

    private string BuildPreview()
    {
        string assembled = _template.BuildAssembledPrompt(_profile, _memorySummary);

        var sb = new StringBuilder();
        sb.AppendLine("[SYSTEM PROMPT]");
        sb.AppendLine(assembled.Trim());
        return sb.ToString();
    }
}
