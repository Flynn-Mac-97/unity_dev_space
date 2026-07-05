"""World-scale normalization: tile = 1 world unit.
- Ground Grid children: localScale 0.5->1, localPosition x2 (elevation offsets).
- All other roots: position x,y x2; scale x,y x2 (cameras/screen-canvases skip scale).
- Player extra bump x1.35 (ends ~1.3u tall vs 1u tile).
- vcam ortho 1->2. World-unit component floats x2 via SerializedObject sweep.
- Runtime-spawn prefab roots x2 (drops, thrown wrench, resource prefabs).
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from unity_bridge import UnityBridge

MUTATE = r"""
    var sb = new System.Text.StringBuilder();
    int scaledRoots = 0;

    System.Action<UnityEditor.SerializedObject, string> d2 = (so, prop) => {
        var p = so.FindProperty(prop);
        if (p == null) return;
        if (p.propertyType == UnityEditor.SerializedPropertyType.Float) p.floatValue *= 2f;
        else if (p.propertyType == UnityEditor.SerializedPropertyType.Vector2) p.vector2Value *= 2f;
        else if (p.propertyType == UnityEditor.SerializedPropertyType.Vector3) {
            var v = p.vector3Value; p.vector3Value = new Vector3(v.x * 2f, v.y * 2f, v.z);
        }
    };
    System.Action<Component, string[]> dbl = (c, props) => {
        if (c == null) return;
        var so = new UnityEditor.SerializedObject(c);
        foreach (var pr in props) d2(so, pr);
        so.ApplyModifiedPropertiesWithoutUndo();
    };

    // ── 1. transforms ────────────────────────────────────────────────
    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    foreach (var root in scene.GetRootGameObjects()) {
        var t = root.transform;
        var p = t.position;
        t.position = new Vector3(p.x * 2f, p.y * 2f, p.z);

        if (root.name == "Ground Grid") {
            foreach (Transform child in t) {
                var lp = child.localPosition;
                child.localPosition = new Vector3(lp.x * 2f, lp.y * 2f, lp.z);
                var ls = child.localScale;
                child.localScale = new Vector3(ls.x * 2f, ls.y * 2f, ls.z);
            }
            sb.AppendLine("Ground Grid children rescaled to 1.0 tile=1u");
            continue;
        }
        if (root.GetComponent<Camera>() != null || root.GetComponent<Cinemachine.CinemachineVirtualCamera>() != null)
            continue; // position moved, no scale
        var canvas = root.GetComponent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            continue; // screen-space UI untouched

        var s = t.localScale;
        t.localScale = new Vector3(s.x * 2f, s.y * 2f, s.z);
        scaledRoots++;
    }
    sb.AppendLine("roots scaled=" + scaledRoots);

    // player bump
    var player = GameObject.Find("Player");
    var ps = player.transform.localScale;
    player.transform.localScale = new Vector3(ps.x * 1.35f, ps.y * 1.35f, ps.z);
    sb.AppendLine("player scale=" + player.transform.localScale.ToString("F2"));

    // camera
    var vcam = GameObject.Find("CM vcam1").GetComponent<Cinemachine.CinemachineVirtualCamera>();
    vcam.m_Lens.OrthographicSize = 2f;
    UnityEditor.EditorUtility.SetDirty(vcam);
    sb.AppendLine("vcam ortho=2");

    // ── 2. component float sweeps ────────────────────────────────────
    dbl(player.GetComponent<Flynn.Player.PlayerController2D>(), new[]{"_moveSpeed","_acceleration","_deceleration"});
    dbl(player.GetComponent<Flynn.Player.PlayerJumpController>(), new[]{"_dashDistance","_arcHeight","_feetOffset","_directionalBias"});
    dbl(player.GetComponent<Flynn.Player.Combat.WrenchController>(), new[]{"_swingRange","_targetRadius"});
    dbl(player.GetComponentInChildren<Flynn.Player.Combat.WrenchChargeFX>(true), new[]{"_ringOffset"});
    dbl(player.GetComponent<Flynn.Player.ExhaustedDebuff>(), new[]{"_barkYOffset"});

    foreach (var c in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true)) {
        var tn = c.GetType().Name;
        switch (tn) {
            case "PlayerHeightState": dbl(c, new[]{"_unitsPerLevel","_shadowFadeRefHeight"}); break;
            case "LandingResolver": dbl(c, new[]{"_probeRadius"}); break;
            case "PlayerScanController": dbl(c, new[]{"_scanRange"}); break;
            case "Interactable": dbl(c, new[]{"_interactionRadius"}); break;
            case "CameraShake": dbl(c, new[]{"_defaultIntensity"}); break;
            case "PodPower": dbl(c, new[]{"_chargeRadius"}); break;
            case "DemoEndTrigger": dbl(c, new[]{"_radius"}); break;
            case "ScriptedTradeNpc": dbl(c, new[]{"_barkYOffset"}); break;
            case "RepairableStructure": dbl(c, new[]{"_barkYOffset"}); break;
            case "NpcBarkController": dbl(c, new[]{"_bubbleOffset","_resourceRange"}); break;
            case "HitImpactFX": dbl(c, new[]{"_particleSpeed","_particleMinSize","_particleMaxSize",
                "_flashStartScale","_flashEndScale","_numberRiseSpeed","_whooshSpeed"}); break;
            case "SlashEffect": dbl(c, new[]{"_startScale","_endScale","_offset","_hitStartScale","_hitEndScale"}); break;
            case "PlayerDropController": dbl(c, new[]{"_popImpulse"}); break;
            case "PillarPushReaction": dbl(c, new[]{"pushOffset"}); break;
            case "DialogueTriggerSealEffect": dbl(c, new[]{"sinkOffset"}); break;
            case "SortableSprite": {
                var so = new UnityEditor.SerializedObject(c);
                var p = so.FindProperty("_elevationSortSteps");
                if (p != null && p.propertyType == UnityEditor.SerializedPropertyType.Float) { p.floatValue *= 0.5f; so.ApplyModifiedPropertiesWithoutUndo(); }
                else if (p != null && p.propertyType == UnityEditor.SerializedPropertyType.Integer) { p.intValue /= 2; so.ApplyModifiedPropertiesWithoutUndo(); }
                break;
            }
        }
    }
    sb.AppendLine("component sweeps done");

    // point lights: radii x2 (skip global)
    int lights = 0;
    foreach (var l in UnityEngine.Object.FindObjectsOfType<UnityEngine.Rendering.Universal.Light2D>(true)) {
        if (l.lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global) continue;
        var so = new UnityEditor.SerializedObject(l);
        var po = so.FindProperty("m_PointLightOuterRadius");
        var pi = so.FindProperty("m_PointLightInnerRadius");
        if (po != null) po.floatValue *= 2f;
        if (pi != null) pi.floatValue *= 2f;
        so.ApplyModifiedPropertiesWithoutUndo();
        lights++;
    }
    sb.AppendLine("point lights radii x2: " + lights);

    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

    // ── 3. prefab roots for runtime spawns ───────────────────────────
    var prefabPaths = new[]{
        "Assets/Flynn/Prefabs/Items/Wood_Drop.prefab","Assets/Flynn/Prefabs/Items/Stone_Drop.prefab",
        "Assets/Flynn/Prefabs/Items/MetalScrap_Drop.prefab","Assets/Flynn/Prefabs/Items/Biomass_Drop.prefab",
        "Assets/Flynn/Prefabs/Items/EchoShard_Drop.prefab","Assets/Flynn/Prefabs/Items/Wrench_Drop.prefab",
        "Assets/Flynn/Prefabs/ThrownWrench.prefab",
        "Assets/Flynn/Prefabs/Resources/Tree.prefab","Assets/Flynn/Prefabs/Resources/Flora.prefab",
        "Assets/Flynn/Prefabs/Resources/Metal_Scrap.prefab"
    };
    int prefabsDone = 0;
    foreach (var path in prefabPaths) {
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
        var contents = UnityEditor.PrefabUtility.LoadPrefabContents(path);
        var s0 = contents.transform.localScale;
        contents.transform.localScale = new Vector3(s0.x * 2f, s0.y * 2f, s0.z);
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(contents, path);
        UnityEditor.PrefabUtility.UnloadPrefabContents(contents);
        prefabsDone++;
    }
    sb.AppendLine("prefab roots x2: " + prefabsDone);

    UnityEditor.AssetDatabase.SaveAssets();
    sb.AppendLine("saved");
    return sb.ToString();
"""


def connect_retry(tries: int = 20, delay: float = 1.5) -> UnityBridge:
    last = None
    for _ in range(tries):
        try:
            b = UnityBridge().connect()
            if b.ping().get("success"):
                return b
        except Exception as e:
            last = e
            time.sleep(delay)
    raise RuntimeError(f"bridge unreachable: {last}")


def main() -> int:
    b = connect_retry()
    try:
        b.csharp("UnityEditor.AssetDatabase.Refresh(); return \"refreshed\";")
    except Exception:
        pass
    finally:
        try:
            b.close()
        except Exception:
            pass

    time.sleep(3)
    receipt = []
    b = connect_retry()
    try:
        for _ in range(8):
            try:
                b.call("manage_editor", "wait_for_compile")
                r = b.csharp(MUTATE)
                receipt.append(str(r.get("result")))
                break
            except Exception as e:
                s = str(e)
                if "reload in progress" in s or "unreachable" in s or "Connection" in s:
                    time.sleep(5)
                    try:
                        b.close()
                    except Exception:
                        pass
                    b = connect_retry()
                else:
                    raise

        con = b.call("read_console", "get", types=["error"], count=10)
        d = (con.get("data") or {}).get("data") or con.get("data") or {}
        ent = d.get("entries") if isinstance(d, dict) else d
        msgs = []
        for e in ent or []:
            m = (e.get("message") if isinstance(e, dict) else str(e)) or ""
            line = m.splitlines()[0][:150]
            if line not in msgs:
                msgs.append(line)
        receipt.append("console: " + ("; ".join(msgs[:6]) if msgs else "clean"))

        shot = b.call("manage_screenshot", "capture_game_view")
        receipt.append("shot: " + str((shot.get("data") or {}).get("path")))
    finally:
        b.close()

    print("\n".join(receipt[:30]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
