using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Editor utility that creates AnimationClips from sprite PNGs and wires an
/// AnimatorController for Flynn's 2.5D 9-state animation system.
/// Run via menu: Flynn → Setup Animations
/// </summary>
public static class FlynnAnimationSetup
{
    private const string AnimOut = "Assets/Flynn/Animations";
    private const float  Fps     = 12f;
    private const string SpBase  = "Assets/Flynn/Sprites/character_animations";

    // ── Entry point ──────────────────────────────────────────────────────────

    [MenuItem("Flynn/Setup Animations")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(AnimOut))
            AssetDatabase.CreateFolder("Assets/Flynn", "Animations");

        var clips = BuildAllClips();
        BuildController(clips);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FlynnAnimationSetup] All done.");
    }

    // ── Clip creation ────────────────────────────────────────────────────────

    private static Dictionary<string, AnimationClip> BuildAllClips()
    {
        // (clipName, folder, looping)
        var defs = new (string name, string folder, bool loop)[]
        {
            ("Flynn_Idle_Front", SpBase + "/positive/idel_02", true),
            ("Flynn_Run_Front",  SpBase + "/positive/run_01",  true),
            ("Flynn_Jump_Front", SpBase + "/positive/jump_02", false),
            ("Flynn_Idle_Back",  SpBase + "/back/idel_01",     true),
            ("Flynn_Run_Back",   SpBase + "/back/run_01",      true),
            ("Flynn_Jump_Back",  SpBase + "/back/jump_01",     false),
            ("Flynn_Idle_Side",  SpBase + "/side/idel_03",     true),
            ("Flynn_Run_Side",   SpBase + "/side/run_03",      true),
            ("Flynn_Jump_Side",  SpBase + "/side/jump_03",     false),
        };

        var map = new Dictionary<string, AnimationClip>();
        foreach (var d in defs)
        {
            var clip = MakeClip(d.name, d.folder, d.loop);
            if (clip != null) map[d.name] = clip;
        }
        return map;
    }

    private static AnimationClip MakeClip(string clipName, string folder, bool loop)
    {
        // Convert asset-relative folder to filesystem path
        string dataPath = Application.dataPath.Replace('\\', '/'); // ends with /Assets
        string sysDir   = dataPath + "/" + folder.Substring("Assets/".Length);

        if (!Directory.Exists(sysDir))
        {
            Debug.LogWarning($"[FlynnAnimationSetup] Folder not found: {sysDir}");
            return null;
        }

        var pngPaths = Directory.GetFiles(sysDir, "*.png")
                                .Select(p => p.Replace('\\', '/'))
                                .OrderBy(Path.GetFileName)
                                .ToArray();

        var sprites = new List<Sprite>(pngPaths.Length);
        foreach (var full in pngPaths)
        {
            string assetPath = "Assets/" + full.Substring(dataPath.Length + 1);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
                sprites.Add(sprite);
            else
                Debug.LogWarning($"[FlynnAnimationSetup] Sprite not found at: {assetPath}");
        }

        if (sprites.Count == 0)
        {
            Debug.LogWarning($"[FlynnAnimationSetup] No sprites loaded for clip '{clipName}'");
            return null;
        }

        var clip = new AnimationClip { frameRate = Fps };

        var binding = new EditorCurveBinding
        {
            type         = typeof(SpriteRenderer),
            path         = "",
            propertyName = "m_Sprite"
        };

        var keys = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        // Loop settings
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;

        // Save asset (replace if already exists)
        string outPath = $"{AnimOut}/{clipName}.anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath) != null)
            AssetDatabase.DeleteAsset(outPath);

        AssetDatabase.CreateAsset(clip, outPath);
        EditorUtility.SetDirty(clip);

        Debug.Log($"[FlynnAnimationSetup] Created '{clipName}'  ({sprites.Count} frames, loop={loop})");
        return clip;
    }

    // ── AnimatorController creation ──────────────────────────────────────────

    private static void BuildController(Dictionary<string, AnimationClip> clips)
    {
        string ctrlPath = $"{AnimOut}/Flynn_AnimatorController.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null)
            AssetDatabase.DeleteAsset(ctrlPath);

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        ctrl.AddParameter("Speed",      AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("FacingDir",  AnimatorControllerParameterType.Int);

        var sm = ctrl.layers[0].stateMachine;

        // Create states
        var idleFront = AddState(sm, clips, "Idle_Front", "Flynn_Idle_Front", new Vector3(300,    0, 0));
        var runFront  = AddState(sm, clips, "Run_Front",  "Flynn_Run_Front",  new Vector3(600,    0, 0));
        var jumpFront = AddState(sm, clips, "Jump_Front", "Flynn_Jump_Front", new Vector3(900,    0, 0));
        var idleBack  = AddState(sm, clips, "Idle_Back",  "Flynn_Idle_Back",  new Vector3(300, -200, 0));
        var runBack   = AddState(sm, clips, "Run_Back",   "Flynn_Run_Back",   new Vector3(600, -200, 0));
        var jumpBack  = AddState(sm, clips, "Jump_Back",  "Flynn_Jump_Back",  new Vector3(900, -200, 0));
        var idleSide  = AddState(sm, clips, "Idle_Side",  "Flynn_Idle_Side",  new Vector3(300,  200, 0));
        var runSide   = AddState(sm, clips, "Run_Side",   "Flynn_Run_Side",   new Vector3(600,  200, 0));
        var jumpSide  = AddState(sm, clips, "Jump_Side",  "Flynn_Jump_Side",  new Vector3(900,  200, 0));

        sm.defaultState = idleFront;

        AnimatorStateTransition t;

        // ── From Idle_Front ──────────────────────────────────────────────────
        t = MkTrans(idleFront, runFront);
        t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        t.AddCondition(AnimatorConditionMode.Equals,  0f,   "FacingDir");

        t = MkTrans(idleFront, jumpFront);
        t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
        t.AddCondition(AnimatorConditionMode.Equals, 0f, "FacingDir");

        t = MkTrans(idleFront, idleBack);
        t.AddCondition(AnimatorConditionMode.Equals, 1f, "FacingDir");

        t = MkTrans(idleFront, idleSide);
        t.AddCondition(AnimatorConditionMode.Equals, 2f, "FacingDir");

        // ── From Run_Front ───────────────────────────────────────────────────
        t = MkTrans(runFront, idleFront);
        t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        t = MkTrans(runFront, idleFront);
        t.AddCondition(AnimatorConditionMode.NotEqual, 0f, "FacingDir");

        t = MkTrans(runFront, jumpFront);
        t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");

        // ── From Jump_Front ──────────────────────────────────────────────────
        t = MkTrans(jumpFront, idleFront);
        t.AddCondition(AnimatorConditionMode.If,     0f,   "IsGrounded");
        t.AddCondition(AnimatorConditionMode.Less,   0.1f, "Speed");
        t.AddCondition(AnimatorConditionMode.Equals, 0f,   "FacingDir");

        t = MkTrans(jumpFront, runFront);
        t.AddCondition(AnimatorConditionMode.If,      0f,   "IsGrounded");
        t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        t.AddCondition(AnimatorConditionMode.Equals,  0f,   "FacingDir");

        // ── From Idle_Back ───────────────────────────────────────────────────
        t = MkTrans(idleBack, runBack);
        t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        t.AddCondition(AnimatorConditionMode.Equals,  1f,   "FacingDir");

        t = MkTrans(idleBack, jumpBack);
        t.AddCondition(AnimatorConditionMode.IfNot,  0f, "IsGrounded");
        t.AddCondition(AnimatorConditionMode.Equals, 1f, "FacingDir");

        t = MkTrans(idleBack, idleFront);
        t.AddCondition(AnimatorConditionMode.Equals, 0f, "FacingDir");

        t = MkTrans(idleBack, idleSide);
        t.AddCondition(AnimatorConditionMode.Equals, 2f, "FacingDir");

        // ── From Run_Back ────────────────────────────────────────────────────
        t = MkTrans(runBack, idleBack);
        t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        t = MkTrans(runBack, idleBack);
        t.AddCondition(AnimatorConditionMode.NotEqual, 1f, "FacingDir");

        t = MkTrans(runBack, jumpBack);
        t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");

        // ── From Jump_Back ───────────────────────────────────────────────────
        t = MkTrans(jumpBack, idleBack);
        t.AddCondition(AnimatorConditionMode.If,     0f,   "IsGrounded");
        t.AddCondition(AnimatorConditionMode.Less,   0.1f, "Speed");
        t.AddCondition(AnimatorConditionMode.Equals, 1f,   "FacingDir");

        t = MkTrans(jumpBack, runBack);
        t.AddCondition(AnimatorConditionMode.If,      0f,   "IsGrounded");
        t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        t.AddCondition(AnimatorConditionMode.Equals,  1f,   "FacingDir");

        // ── From Idle_Side ───────────────────────────────────────────────────
        t = MkTrans(idleSide, runSide);
        t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        t.AddCondition(AnimatorConditionMode.Equals,  2f,   "FacingDir");

        t = MkTrans(idleSide, jumpSide);
        t.AddCondition(AnimatorConditionMode.IfNot,  0f, "IsGrounded");
        t.AddCondition(AnimatorConditionMode.Equals, 2f, "FacingDir");

        t = MkTrans(idleSide, idleFront);
        t.AddCondition(AnimatorConditionMode.Equals, 0f, "FacingDir");

        t = MkTrans(idleSide, idleBack);
        t.AddCondition(AnimatorConditionMode.Equals, 1f, "FacingDir");

        // ── From Run_Side ────────────────────────────────────────────────────
        t = MkTrans(runSide, idleSide);
        t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        t = MkTrans(runSide, idleSide);
        t.AddCondition(AnimatorConditionMode.NotEqual, 2f, "FacingDir");

        t = MkTrans(runSide, jumpSide);
        t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");

        // ── From Jump_Side ───────────────────────────────────────────────────
        t = MkTrans(jumpSide, idleSide);
        t.AddCondition(AnimatorConditionMode.If,     0f,   "IsGrounded");
        t.AddCondition(AnimatorConditionMode.Less,   0.1f, "Speed");
        t.AddCondition(AnimatorConditionMode.Equals, 2f,   "FacingDir");

        t = MkTrans(jumpSide, runSide);
        t.AddCondition(AnimatorConditionMode.If,      0f,   "IsGrounded");
        t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        t.AddCondition(AnimatorConditionMode.Equals,  2f,   "FacingDir");

        EditorUtility.SetDirty(ctrl);
        Debug.Log("[FlynnAnimationSetup] AnimatorController created.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
