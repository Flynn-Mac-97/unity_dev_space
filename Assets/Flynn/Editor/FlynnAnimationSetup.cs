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

    // ── Placeholder diagonal clips (single sprite, decoupled from controller) ──
    //
    // 8-direction support adds FacingDir 3 = back-diagonal (NE/NW). Until real
    // diagonal animations exist, these clips just hold one frame. This menu creates
    // ONLY the clips and leaves Flynn_AnimatorController untouched — wire the
    // FacingDir==3 states/transitions by hand (or extend BuildController later).

    private const string NewCharDir = SpBase + "/New Character";

    // FacingDir → single placeholder sprite (8-direction via flipX for the L/R pair):
    //   0 front, 1 back, 2 side, 3 back-diagonal (NE/NW), 4 front-diagonal (SE/SW).
    private static readonly (string suffix, string sprite)[] PlaceholderDirs =
    {
        ("Front",    "character_front_orth.png"),
        ("Back",     "character_back_orth.png"),
        ("Side",     "character_right.png"),
        ("BackDiag", "character_45_back_orth.png"),
        ("FrontDiag","character_45_orth.png"),
    };

    /// <summary>
    /// Sets every locomotion clip (Idle/Run/Jump × 5 directions) to a SINGLE frame of the
    /// matching New Character sprite. Existing clips are rewritten in place (GUID preserved,
    /// so AnimatorController state references survive); missing ones are created. The
    /// AnimatorController is NOT touched — wire the FacingDir==3/4 diagonal states by hand.
    /// </summary>
    [MenuItem("Flynn/Set Placeholder Sprites (single frame)")]
    public static void SetPlaceholderSprites()
    {
        if (!AssetDatabase.IsValidFolder(AnimOut))
            AssetDatabase.CreateFolder("Assets/Flynn", "Animations");

        int ok = 0, total = 0;
        foreach (var (suffix, spriteFile) in PlaceholderDirs)
        {
            string spritePath = $"{NewCharDir}/{spriteFile}";
            foreach (var (prefix, loop) in new[] { ("Idle", true), ("Run", true), ("Jump", false) })
            {
                total++;
                if (SetSingleSpriteClip($"Flynn_{prefix}_{suffix}", spritePath, loop) != null) ok++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FlynnAnimationSetup] Placeholder sprites set: {ok}/{total} clips. " +
                  "Controller untouched — wire FacingDir==3 (back-diag) & ==4 (front-diag) states manually.");
    }

    /// <summary>
    /// Forces a clip to a one-frame sprite animation. Loads the existing asset (preserving its
    /// GUID + controller references) or creates it if absent, then overwrites the m_Sprite curve.
    /// </summary>
    private static AnimationClip SetSingleSpriteClip(string clipName, string spritePath, bool loop)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[FlynnAnimationSetup] Sprite not found (imported as a Sprite?): {spritePath}");
            return null;
        }

        string outPath = $"{AnimOut}/{clipName}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
        bool existed = clip != null;
        if (!existed) clip = new AnimationClip();
        clip.frameRate = Fps;

        var binding = new EditorCurveBinding
        {
            type         = typeof(SpriteRenderer),
            path         = "",
            propertyName = "m_Sprite"
        };
        // Single keyframe replaces any prior multi-frame curve for this binding.
        var keys = new[] { new ObjectReferenceKeyframe { time = 0f, value = sprite } };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;

        if (!existed) AssetDatabase.CreateAsset(clip, outPath);
        EditorUtility.SetDirty(clip);

        Debug.Log($"[FlynnAnimationSetup] {(existed ? "Rewrote" : "Created")} '{clipName}' → {sprite.name} (1 frame, loop={loop}).");
        return clip;
    }

    // ── Clip creation ────────────────────────────────────────────────────────

    private static Dictionary<string, AnimationClip> BuildAllClips()
    {
        // (clipName, folder, looping)
        var defs = new (string name, string folder, bool loop)[]
        {
            ("Flynn_Idle_Front",  SpBase + "/positive/idel_02", true),
            ("Flynn_Run_Front",   SpBase + "/positive/run_01",  true),
            ("Flynn_Jump_Front",  SpBase + "/positive/jump_02", false),
            ("Flynn_Idle_Back",   SpBase + "/back/idel_01",     true),
            ("Flynn_Run_Back",    SpBase + "/back/run_01",      true),
            ("Flynn_Jump_Back",   SpBase + "/back/jump_01",     false),
            ("Flynn_Idle_Side",   SpBase + "/side/idel_03",     true),
            ("Flynn_Run_Side",    SpBase + "/side/run_03",      true),
            ("Flynn_Jump_Side",   SpBase + "/side/jump_03",     false),
            // ── Attack animations (one-shot, tool-specific) ──────────────
            ("Flynn_attack_01",   SpBase + "/attack_01",        false),  // pick
            ("Flynn_attack_02",   SpBase + "/attack_02",        false),  // axe
            ("Flynn_attack_03",   SpBase + "/attack_03",        false),  // hammer
            ("Flynn_attack_04",   SpBase + "/attack_04",        false),  // wrench
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
        ctrl.AddParameter("Speed",       AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsGrounded",  AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("FacingDir",   AnimatorControllerParameterType.Int);
        ctrl.AddParameter("Attack",      AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);

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

        // ── Attack states (AnyState → Attack_N, then → Idle_Front on exit) ─────────────
        var attackDefs = new (string state, string clip, int index)[]{
            ("Attack_Pick",   "Flynn_attack_01", 1),
            ("Attack_Axe",    "Flynn_attack_02", 2),
            ("Attack_Hammer", "Flynn_attack_03", 3),
            ("Attack_Wrench", "Flynn_attack_04", 4),
        };

        for (int ai = 0; ai < attackDefs.Length; ai++)
        {
            var (stateName, clipName, animIndex) = attackDefs[ai];
            var attackState = AddState(sm, clips, stateName, clipName,
                                       new Vector3(300 + ai * 300, -500, 0));
            // Tag so FlynnAnimationDriver.IsAttacking can detect the attack state (its
            // double-fire guard and the swing release→idle handoff both depend on this).
            attackState.tag = "Attack";

            // AnyState → attack: trigger fired AND AttackIndex matches
            var anyTrans = sm.AddAnyStateTransition(attackState);
            anyTrans.hasExitTime       = false;
            anyTrans.duration          = 0f;
            anyTrans.canTransitionToSelf = false;
            anyTrans.AddCondition(AnimatorConditionMode.If,     0f,          "Attack");
            anyTrans.AddCondition(AnimatorConditionMode.Equals, animIndex,   "AttackIndex");

            // Attack → Idle_Front once the clip has mostly played (85% through)
            var exitTrans = attackState.AddTransition(idleFront);
            exitTrans.hasExitTime = true;
            exitTrans.exitTime    = 0.85f;
            exitTrans.duration    = 0f;
        }

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
