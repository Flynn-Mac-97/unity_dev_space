using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Editor utility that builds Flynn's AnimatorController for an 8-direction animation system
/// (Down, DownLeft, Left, UpLeft, Up, UpRight, Right, DownRight).
/// Clips are generated from Character_ANIM_Sheets sprite sheets (16 cols × 8 rows).
/// Run via menu: Flynn → Setup Animations
/// </summary>
public static class FlynnAnimationSetup
{
    private const string AnimOut   = "Assets/Flynn/Animations";
    private const string SheetBase = "Assets/Flynn/Sprites/Character_ANIM_Sheets";
    private const float  Fps       = 24f;
    private const int    Cols      = 16;

    private static readonly string[] DirNames =
    {
        "Down", "DownLeft", "Left", "UpLeft", "Up", "UpRight", "Right", "DownRight"
    };

    // Sheet definitions: (type name, sheet filename, loop, output subfolder, cell height)
    private static readonly (string type, string file, bool loop, string folder, int cellH)[] SheetDefs =
    {
        ("Idle",  "idle.png",  true,  "Idle8Dir",  52),
        ("Run",   "run.png",   true,  "Run8Dir",   53),
        ("Swing", "swing.png", false, "Swing8Dir", 59),
    };

    // ── Entry point ──────────────────────────────────────────────────────

    [MenuItem("Flynn/Setup Animations")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(AnimOut))
            AssetDatabase.CreateFolder("Assets/Flynn", "Animations");

        SliceSheets();
        var clips = BuildAllClips();
        BuildController(clips);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FlynnAnimationSetup] All done — 8-direction controller built.");
    }

    // ── Sprite sheet slicing ─────────────────────────────────────────────

    private static void SliceSheets()
    {
        int rows = DirNames.Length;
        int cellWidth = 128; // 2048 / 16

        foreach (var (_, file, _, _, cellH) in SheetDefs)
        {
            string path = $"{SheetBase}/{file}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogError($"No importer: {path}"); continue; }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsToUnits = 100;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var spritesList = new List<SpriteMetaData>();
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    spritesList.Add(new SpriteMetaData
                    {
                        name = $"{System.IO.Path.GetFileNameWithoutExtension(file)}_{row * Cols + col}",
                        rect = new Rect(col * cellWidth, (rows - 1 - row) * cellH, cellWidth, cellH),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    });
                }
            }

            importer.spritesheet = spritesList.ToArray();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            Debug.Log($"[FlynnAnimationSetup] Sliced {path} → {spritesList.Count} sprites");
        }

        // Set _n variants to NormalMap (unsliced)
        string[] normalSheets = { "idle_n.png", "run_n.png", "swing_n.png" };
        foreach (var nFile in normalSheets)
        {
            string nPath = $"{SheetBase}/{nFile}";
            var nImporter = AssetImporter.GetAtPath(nPath) as TextureImporter;
            if (nImporter == null) continue;

            nImporter.textureType = TextureImporterType.NormalMap;
            nImporter.spriteImportMode = SpriteImportMode.None;
            nImporter.textureCompression = TextureImporterCompression.Uncompressed;
            nImporter.filterMode = FilterMode.Bilinear;
            EditorUtility.SetDirty(nImporter);
            nImporter.SaveAndReimport();

            Debug.Log($"[FlynnAnimationSetup] {nPath} → NormalMap");
        }
    }

    // ── Clip creation ────────────────────────────────────────────────────

    private static Dictionary<string, AnimationClip> BuildAllClips()
    {
        var map = new Dictionary<string, AnimationClip>();

        foreach (var (type, file, loop, folder, _) in SheetDefs)
        {
            string sheetPath = $"{SheetBase}/{file}";
            var allObjects = AssetDatabase.LoadAllAssetRepresentationsAtPath(sheetPath);
            var allSprites = allObjects.OfType<Sprite>().ToList();

            if (!AssetDatabase.IsValidFolder($"{AnimOut}/{folder}"))
                AssetDatabase.CreateFolder(AnimOut, folder);

            for (int dirIdx = 0; dirIdx < DirNames.Length; dirIdx++)
            {
                string dir = DirNames[dirIdx];
                string clipName = $"Flynn_{type}_{dir}";
                string outPath = $"{AnimOut}/{folder}/{clipName}.anim";

                // Collect sprites for this direction row (sequential naming: row 0 = sprites 0-15, etc.)
                int rowStart = dirIdx * Cols;
                var dirSprites = new List<Sprite>();
                for (int col = 0; col < Cols; col++)
                {
                    var sprite = allSprites[rowStart + col];
                    if (sprite != null) dirSprites.Add(sprite);
                }

                if (dirSprites.Count == 0)
                {
                    Debug.LogWarning($"[FlynnAnimationSetup] No sprites for {clipName}");
                    continue;
                }

                // Delete existing clip to recreate with new frame count
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath) != null)
                    AssetDatabase.DeleteAsset(outPath);

                var clip = new AnimationClip { frameRate = Fps };

                var binding = new EditorCurveBinding
                {
                    type = typeof(SpriteRenderer),
                    path = "",
                    propertyName = "m_Sprite"
                };

                var keys = new ObjectReferenceKeyframe[dirSprites.Count];
                for (int i = 0; i < dirSprites.Count; i++)
                    keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = dirSprites[i] };

                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;

                AssetDatabase.CreateAsset(clip, outPath);
                EditorUtility.SetDirty(clip);
                map[clipName] = clip;

                Debug.Log($"[FlynnAnimationSetup] Created '{clipName}' ({dirSprites.Count} frames, loop={loop})");
            }
        }

        return map;
    }

    // ── AnimatorController creation ──────────────────────────────────────

    private static void BuildController(Dictionary<string, AnimationClip> clips)
    {
        string ctrlPath = $"{AnimOut}/Flynn_AnimatorController.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null)
            AssetDatabase.DeleteAsset(ctrlPath);

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        ctrl.AddParameter("Speed",      AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("FacingDir",  AnimatorControllerParameterType.Int);
        ctrl.AddParameter("Jump",       AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Swing",      AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Hold",        AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Throw",      AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Scan",       AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;
        var states = new Dictionary<string, AnimatorState>();

        // Locomotion: Idle & Run per direction
        float xBase = 0f;
        foreach (var dir in DirNames)
        {
            var idle = AddState(sm, clips, $"Idle_{dir}", $"Flynn_Idle_{dir}", new Vector3(xBase, 0, 0));
            var run  = AddState(sm, clips, $"Run_{dir}",  $"Flynn_Run_{dir}",  new Vector3(xBase + 300, 0, 0));
            states[$"Idle_{dir}"] = idle;
            states[$"Run_{dir}"]  = run;
            xBase += 400f;
        }

        sm.defaultState = states["Idle_Down"];

        // Swing states (bottom row)
        float xSwing = 0f;
        float ySwing = -300f;
        foreach (var dir in DirNames)
        {
            var swing = AddState(sm, clips, $"Swing_{dir}", $"Flynn_Swing_{dir}", new Vector3(xSwing, ySwing, 0));
            swing.tag = "Attack";
            states[$"Swing_{dir}"] = swing;
            xSwing += 300f;
        }

        // ── Locomotion transitions within each direction ──────────────────

        foreach (var dir in DirNames)
        {
            int dirVal = System.Array.IndexOf(DirNames, dir);
            var idle = states[$"Idle_{dir}"];
            var run  = states[$"Run_{dir}"];

            // Idle → Run
            var t = MkTrans(idle, run);
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t.AddCondition(AnimatorConditionMode.Equals, dirVal, "FacingDir");

            // Run → Idle (speed drops)
            t = MkTrans(run, idle);
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Run → Idle (direction changed)
            t = MkTrans(run, idle);
            t.AddCondition(AnimatorConditionMode.NotEqual, dirVal, "FacingDir");
        }

        // ── Cross-direction idle transitions ─────────────────────────────

        for (int i = 0; i < DirNames.Length; i++)
        {
            for (int j = 0; j < DirNames.Length; j++)
            {
                if (i == j) continue;
                var t = MkTrans(states[$"Idle_{DirNames[i]}"], states[$"Idle_{DirNames[j]}"]);
                t.AddCondition(AnimatorConditionMode.Equals, j, "FacingDir");
            }
        }

        // ── Swing: AnyState → Swing per direction, Swing → Idle ───────────

        foreach (var dir in DirNames)
        {
            int dirVal = System.Array.IndexOf(DirNames, dir);
            var swingState = states[$"Swing_{dir}"];

            var anyTrans = sm.AddAnyStateTransition(swingState);
            anyTrans.hasExitTime = false;
            anyTrans.duration = 0f;
            anyTrans.canTransitionToSelf = false;
            anyTrans.AddCondition(AnimatorConditionMode.If, 0f, "Swing");
            anyTrans.AddCondition(AnimatorConditionMode.Equals, dirVal, "FacingDir");

            var exitTrans = swingState.AddTransition(states[$"Idle_{dir}"]);
            exitTrans.hasExitTime = true;
            exitTrans.exitTime = 0.85f;
            exitTrans.duration = 0.15f;
        }

        EditorUtility.SetDirty(ctrl);
        Debug.Log("[FlynnAnimationSetup] AnimatorController created with 8-direction support.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static AnimatorState AddState(
        AnimatorStateMachine sm,
        Dictionary<string, AnimationClip> clips,
        string stateName,
        string clipName,
        Vector3 position)
    {
        var state = sm.AddState(stateName, position);
        if (clips.TryGetValue(clipName, out var clip))
            state.motion = clip;
        else
            Debug.LogWarning($"[FlynnAnimationSetup] Clip '{clipName}' not found for state '{stateName}'");
        return state;
    }

    private static AnimatorStateTransition MkTrans(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = 0f;
        t.exitTime    = 0f;
        return t;
    }
}
