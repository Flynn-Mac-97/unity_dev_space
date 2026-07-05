using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using BlendTree = UnityEditor.Animations.BlendTree;

/// <summary>
/// Builds Flynn's 16-direction animation system: import settings, 112 clips,
/// and an AnimatorController with 2D Simple Directional blend trees.
/// Run via menu: Flynn → Setup 16-Direction Animations
/// </summary>
public static class FlynnAnimationSetup16Dir
{
    private const string SpriteRoot    = "Assets/Flynn/Sprites/character_animations";
    private const string AnimOut       = "Assets/Flynn/Animations/16Dir";
    private const string ControllerPath = "Assets/Flynn/Animations/Flynn.controller";
    private const float  Fps           = 12f;
    private const int    PPU           = 256;

    // 16 direction unit vectors (MoveX, MoveY), counter-clockwise from front
    private static readonly Vector2[] DirVectors =
    {
        new Vector2( 0.000f, -1.000f), // dir00 front
        new Vector2( 0.383f, -0.924f), // dir01
        new Vector2( 0.707f, -0.707f), // dir02
        new Vector2( 0.924f, -0.383f), // dir03
        new Vector2( 1.000f,  0.000f), // dir04 right
        new Vector2( 0.924f,  0.383f), // dir05
        new Vector2( 0.707f,  0.707f), // dir06
        new Vector2( 0.383f,  0.924f), // dir07
        new Vector2( 0.000f,  1.000f), // dir08 back
        new Vector2(-0.383f,  0.924f), // dir09
        new Vector2(-0.707f,  0.707f), // dir10
        new Vector2(-0.924f,  0.383f), // dir11
        new Vector2(-1.000f,  0.000f), // dir12 left
        new Vector2(-0.924f, -0.383f), // dir13
        new Vector2(-0.707f, -0.707f), // dir14
        new Vector2(-0.383f, -0.924f), // dir15
    };

    // (folder name, loop)
    private static readonly (string name, bool loop)[] AnimDefs =
    {
        ("idle",        true),
        ("run",         true),
        ("carry_heavy", true),
        ("swim",        true),
        ("grapple_fly", true),
        ("jump",        false),
        ("throw",       false),
    };

    [MenuItem("Flynn/Setup 16-Direction Animations")]
    public static void Run()
    {
        EditorUtility.DisplayProgressBar("16-Dir Setup", "Setting texture import settings...", 0f);
        SetupImportSettings();

        EditorUtility.DisplayProgressBar("16-Dir Setup", "Creating animation clips...", 0.3f);
        var clips = CreateAllClips();

        EditorUtility.DisplayProgressBar("16-Dir Setup", "Building animator controller...", 0.7f);
        BuildController(clips);

        EditorUtility.ClearProgressBar();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[16-Dir] Complete! 112 clips + Flynn.controller created.");
    }

    // ── Step 1: Import settings ─────────────────────────────────────────

    private static void SetupImportSettings()
    {
        var absRoot = ToAbsPath(SpriteRoot);
        var allPngs = Directory.GetFiles(absRoot, "*.png", SearchOption.AllDirectories)
                               .OrderBy(p => p)
                               .ToList();

        int changed = 0;
        for (int i = 0; i < allPngs.Count; i++)
        {
            if (i % 50 == 0)
                EditorUtility.DisplayProgressBar("Import Settings",
                    $"Texture {i}/{allPngs.Count}", (float)i / allPngs.Count * 0.3f);

            var rel = ToAssetPath(allPngs[i]);
            var imp = AssetImporter.GetAtPath(rel) as TextureImporter;
            if (imp == null) continue;

            bool dirty = false;
            if (imp.textureType        != TextureImporterType.Sprite)          { imp.textureType        = TextureImporterType.Sprite;            dirty = true; }
            if (imp.spriteImportMode   != SpriteImportMode.Single)              { imp.spriteImportMode   = SpriteImportMode.Single;              dirty = true; }
            if (imp.spritePixelsToUnits != PPU)                                 { imp.spritePixelsToUnits = PPU;                                    dirty = true; }
            if (imp.filterMode         != FilterMode.Bilinear)                  { imp.filterMode         = FilterMode.Bilinear;                    dirty = true; }
            if (imp.textureCompression != TextureImporterCompression.Uncompressed){ imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (imp.maxTextureSize     != 256)                                  { imp.maxTextureSize     = 256;                                     dirty = true; }

            if (dirty)
            {
                imp.SaveAndReimport();
                changed++;
            }
        }

        Debug.Log($"[16-Dir] Import settings: {changed}/{allPngs.Count} textures updated (PPU={PPU}, Uncompressed, maxSize=256)");
    }

    // ── Step 2: Create clips ────────────────────────────────────────────

    private static Dictionary<string, AnimationClip> CreateAllClips()
    {
        var map = new Dictionary<string, AnimationClip>();
        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        foreach (var (animName, loop) in AnimDefs)
        {
            // PascalCase folder name for output
            var pascalName = ToPascalCase(animName);
            var outFolder  = $"{AnimOut}/{pascalName}";
            EnsureFolder(outFolder);

            for (int dir = 0; dir < 16; dir++)
            {
                var dirName  = $"dir{dir:D2}";
                var dirAsset = $"{SpriteRoot}/{animName}/{dirName}";
                var clipName = $"{animName}_dir{dir:D2}";
                var clipPath = $"{outFolder}/{clipName}.anim";

                // Load sprites (sorted by filename)
                var absDir = ToAbsPath(dirAsset);
                var pngFiles = Directory.GetFiles(absDir, "*.png")
                                       .OrderBy(p => Path.GetFileName(p))
                                       .ToList();

                var sprites = new List<Sprite>();
                foreach (var png in pngFiles)
                {
                    var sp = AssetDatabase.LoadAssetAtPath<Sprite>(ToAssetPath(png));
                    if (sp != null) sprites.Add(sp);
                }

                if (sprites.Count == 0)
                {
                    Debug.LogWarning($"[16-Dir] No sprites at {dirAsset}");
                    continue;
                }

                // Replace existing clip
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
                    AssetDatabase.DeleteAsset(clipPath);

                var clip = new AnimationClip { frameRate = Fps };

                var keys = new ObjectReferenceKeyframe[sprites.Count];
                for (int f = 0; f < sprites.Count; f++)
                    keys[f] = new ObjectReferenceKeyframe { time = f / Fps, value = sprites[f] };

                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;

                AssetDatabase.CreateAsset(clip, clipPath);
                map[clipName] = clip;
            }

            EditorUtility.DisplayProgressBar("16-Dir Setup",
                $"Creating clips... {pascalName} done", 0.3f + 0.4f * (ArrayIndexOf(AnimDefs, animName) + 1f) / AnimDefs.Length);
        }

        Debug.Log($"[16-Dir] Created {map.Count} animation clips at {Fps} fps");
        return map;
    }

    // ── Step 3: Build controller ────────────────────────────────────────

    private static void BuildController(Dictionary<string, AnimationClip> clips)
    {
        // Replace existing controller
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // Parameters
        ctrl.AddParameter("MoveX",     AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveY",     AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Speed",     AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Carrying",   AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Swimming",   AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Grappling",  AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Jump",      AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Throw",     AnimatorControllerParameterType.Trigger);

        // Create per-anim 2D Simple Directional blend trees
        var trees = new Dictionary<string, BlendTree>();
        foreach (var (animName, _) in AnimDefs)
        {
            var tree = CreateDirBlendTree(ctrl, clips, animName);
            trees[animName] = tree;
        }

        // Locomotion: 1D blend (Speed) → idle at 0, run at 1
        var locoTree = new BlendTree { name = "Locomotion" };
        locoTree.blendType      = BlendTreeType.Simple1D;
        locoTree.blendParameter = "Speed";

        var locoChildren = new ChildMotion[]
        {
            new ChildMotion { motion = trees["idle"], threshold = 0f, position = Vector2.zero, timeScale = 1f },
            new ChildMotion { motion = trees["run"],  threshold = 1f, position = Vector2.zero, timeScale = 1f },
        };
        locoTree.children = locoChildren;
        AssetDatabase.AddObjectToAsset(locoTree, ctrl);

        // State machine
        var sm = ctrl.layers[0].stateMachine;

        // States
        var locoState    = sm.AddState("Locomotion", new Vector3(0,    0,   0));
        locoState.motion = locoTree;

        var carryState    = sm.AddState("CarryHeavy",  new Vector3(450,   0, 0));
        carryState.motion = trees["carry_heavy"];

        var swimState    = sm.AddState("Swim",        new Vector3(450, -120, 0));
        swimState.motion = trees["swim"];

        var grappleState    = sm.AddState("GrappleFly",  new Vector3(450, -240, 0));
        grappleState.motion = trees["grapple_fly"];

        var jumpState    = sm.AddState("Jump",        new Vector3(0,  -200, 0));
        jumpState.motion = trees["jump"];

        var throwState    = sm.AddState("Throw",       new Vector3(0,  -400, 0));
        throwState.motion = trees["throw"];

        sm.defaultState = locoState;

        // ── Transitions ────────────────────────────────────────────────

        // Locomotion ⇄ CarryHeavy  (bool Carrying)
        AddBoolTransition(locoState, carryState, "Carrying", true);
        AddBoolTransition(carryState, locoState, "Carrying", false);

        // Locomotion ⇄ Swim  (bool Swimming)
        AddBoolTransition(locoState, swimState, "Swimming", true);
        AddBoolTransition(swimState, locoState, "Swimming", false);

        // Locomotion ⇄ GrappleFly  (bool Grappling)
        AddBoolTransition(locoState, grappleState, "Grappling", true);
        AddBoolTransition(grappleState, locoState, "Grappling", false);

        // Locomotion → Jump (trigger) → Locomotion (exit time)
        AddTriggerTransition(locoState, jumpState, "Jump");
        AddExitTimeTransition(jumpState, locoState, 0.9f);

        // Locomotion → Throw (trigger) → Locomotion (exit time)
        AddTriggerTransition(locoState, throwState, "Throw");
        AddExitTimeTransition(throwState, locoState, 0.9f);

        EditorUtility.SetDirty(ctrl);
        Debug.Log("[16-Dir] AnimatorController 'Flynn' built with 6 states, 8 blend trees.");
    }

    // ── Blend tree helper ───────────────────────────────────────────────

    private static BlendTree CreateDirBlendTree(
        AnimatorController ctrl,
        Dictionary<string, AnimationClip> clips,
        string animName)
    {
        var tree = new BlendTree { name = animName };
        tree.blendType      = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = "MoveX";
        tree.blendParameterY = "MoveY";

        var children = new ChildMotion[16];
        for (int dir = 0; dir < 16; dir++)
        {
            var clipKey = $"{animName}_dir{dir:D2}";
            if (!clips.TryGetValue(clipKey, out var clip))
            {
                Debug.LogWarning($"[16-Dir] Missing clip: {clipKey}");
                continue;
            }
            children[dir] = new ChildMotion
            {
                motion    = clip,
                position  = DirVectors[dir],
                threshold = (float)dir / 15f,
                timeScale = 1f
            };
        }
        tree.children = children;
        AssetDatabase.AddObjectToAsset(tree, ctrl);
        return tree;
    }

    // ── Transition helpers ──────────────────────────────────────────────

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string param, bool value)
    {
        var t = from.AddTransition(to);
        t.hasExitTime  = false;
        t.duration     = 0.1f;
        t.exitTime     = 0f;
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
    }

    private static void AddTriggerTransition(AnimatorState from, AnimatorState to, string trigger)
    {
        var t = from.AddTransition(to);
        t.hasExitTime  = false;
        t.duration     = 0f;
        t.exitTime     = 0f;
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
    }

    private static void AddExitTimeTransition(AnimatorState from, AnimatorState to, float exitTime)
    {
        var t = from.AddTransition(to);
        t.hasExitTime  = true;
        t.exitTime     = exitTime;
        t.duration     = 0.15f;
    }

    // ── Path / utility helpers ──────────────────────────────────────────

    private static string ToAbsPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }

    private static string ToAssetPath(string absPath)
    {
        var dataPath = Application.dataPath.Replace('/', Path.DirectorySeparatorChar);
        if (absPath.StartsWith(dataPath))
            return "Assets" + absPath.Substring(dataPath.Length).Replace('\\', '/');
        return absPath.Replace('\\', '/');
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;
        var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        var name   = Path.GetFileName(assetPath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) return;
        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(parent))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(parent)?.Replace('\\', '/'), Path.GetFileName(parent));
        if (!AssetDatabase.IsValidFolder(assetPath))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var parts = s.Split('_');
        return string.Join("", parts.Select(p => char.ToUpper(p[0]) + p.Substring(1)));
    }

    private static int ArrayIndexOf((string name, bool loop)[] arr, string name)
    {
        for (int i = 0; i < arr.Length; i++)
            if (arr[i].name == name) return i;
        return -1;
    }
}
