"""Wrench feature wiring flush — single batched job.

1. Refresh + compile the 6 new/edited scripts, abort on compile errors.
2. Relink orphaned PowerBuildupSettings.asset to the recreated class (guid patch).
3. Wire Player (WrenchController + WrenchChargeFX + refs), ensure HitStop/HitImpactFX.
4. Save scene, report console. Receipt only — no raw JSON.
"""
import os
import re
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from unity_bridge import UnityBridge

ASSET = "Assets/Flynn/Configs/Player/PowerBuildupSettings.asset"
META = "Assets/Flynn/Scripts/Player/Combat/PowerBuildupSettings.cs.meta"


def connect(retries=20, delay=2.0):
    last = None
    for _ in range(retries):
        try:
            return UnityBridge().connect()
        except (ConnectionRefusedError, OSError, Exception) as e:  # port may rotate during reload
            last = e
            time.sleep(delay)
    raise SystemExit(f"BRIDGE DOWN — open the Unity editor ({last})")


def wait_idle(b, timeout=180):
    t0 = time.time()
    while time.time() - t0 < timeout:
        try:
            r = b.call("manage_editor", "get_state")
            d = (r.get("data") or {}).get("data") or {}
            if not d.get("isCompiling") and not d.get("isUpdating"):
                return b, d
        except (ConnectionResetError, ConnectionRefusedError, OSError, Exception):
            try:
                b.close()
            except Exception:
                pass
            time.sleep(2)
            b = connect()
        time.sleep(1.5)
    raise SystemExit("TIMEOUT waiting for compile")


def csharp(b, code):
    r = b.csharp(code)
    return r.get("result"), r.get("logs") or []


def main():
    receipt = []
    b = connect()

    # ── 1. import + compile ───────────────────────────────────────────────
    try:
        b.csharp("UnityEditor.AssetDatabase.Refresh(); return \"ok\";")
    except Exception:
        pass  # refresh can kick an immediate reload that drops the socket
    try:
        b.close()
    except Exception:
        pass
    time.sleep(3)
    b = connect()
    b, _ = wait_idle(b)

    errs, _ = csharp(b, r"""
        var log = new System.Text.StringBuilder();
        return UnityEditor.EditorUtility.scriptCompilationFailed ? "COMPILE_FAILED" : "clean";
    """)
    if errs == "COMPILE_FAILED":
        # pull first errors and bail
        result, _ = csharp(b, r"""
            var entries = new System.Collections.Generic.List<string>();
            var t = System.Type.GetType("UnityEditor.LogEntries,UnityEditor");
            return "see console";
        """)
        r = b.call("read_console", "error")
        print("COMPILE FAILED — first console payload:")
        print(str(r.get("data"))[:1500])
        return 1
    receipt.append("compile: clean")

    # ── 2. relink settings asset guid ─────────────────────────────────────
    try:
        with open(META, encoding="utf-8") as f:
            m = re.search(r"guid:\s*([0-9a-f]{32})", f.read())
        if not m:
            raise SystemExit("no guid in new meta")
        new_guid = m.group(1)
        with open(ASSET, encoding="utf-8") as f:
            text = f.read()
        old = re.search(r"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})", text)
        if old and old.group(1) != new_guid:
            text = text.replace(old.group(1), new_guid)
            with open(ASSET, "w", encoding="utf-8", newline="\n") as f:
                f.write(text)
            b.call("manage_asset", "import", path=ASSET)
            receipt.append(f"asset relinked: {old.group(1)[:8]} -> {new_guid[:8]}")
        else:
            receipt.append("asset relink: already linked" if old else "asset relink: NO m_Script LINE?")
    except FileNotFoundError as e:
        receipt.append(f"asset relink FAILED: {e}")

    b, _ = wait_idle(b)

    # ── 3. scene wiring (single editor-side C# pass) ──────────────────────
    result, logs = csharp(b, r"""
        var sb = new System.Text.StringBuilder();
        var asm = System.AppDomain.CurrentDomain.GetAssemblies();
        System.Func<string, System.Type> T = (name) => {
            foreach (var a in asm) { var t = a.GetType(name); if (t != null) return t; }
            return null;
        };

        var pcT = T("Flynn.Player.PlayerController2D");
        var pc = UnityEngine.Object.FindObjectOfType(pcT) as UnityEngine.MonoBehaviour;
        if (pc == null) return "FAIL: no PlayerController2D in scene";
        var player = pc.gameObject;
        sb.Append("player=" + player.name);

        // wrench pivot: first descendant with 'wrench' in its name
        UnityEngine.Transform pivot = null;
        foreach (var tr in player.GetComponentsInChildren<UnityEngine.Transform>(true))
            if (tr != player.transform && tr.name.ToLower().Contains("wrench")) { pivot = tr; break; }
        sb.Append(" pivot=" + (pivot ? pivot.name : "NONE"));

        var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.ScriptableObject>(
            "Assets/Flynn/Configs/Player/PowerBuildupSettings.asset");
        sb.Append(" settings=" + (settings ? "ok" : "NULL"));

        // sorting config asset (optional)
        UnityEngine.Object sortCfg = null;
        var guids = UnityEditor.AssetDatabase.FindAssets("t:SpriteSortingConfigSO");
        if (guids.Length > 0)
            sortCfg = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.ScriptableObject>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));

        System.Func<UnityEngine.GameObject, System.Type, UnityEngine.Component> ensure = (go, t) => {
            var c = go.GetComponent(t); if (c == null) c = go.AddComponent(t); return c;
        };

        var wcT = T("Flynn.Player.Combat.WrenchController");
        var fxT = T("Flynn.Player.Combat.WrenchChargeFX");
        if (wcT == null || fxT == null) return "FAIL: combat types missing (compile?)";

        var wc = ensure(player, wcT);
        var so = new UnityEditor.SerializedObject(wc);
        so.FindProperty("_settings").objectReferenceValue = settings;
        so.FindProperty("_wrenchPivot").objectReferenceValue = pivot ? pivot.gameObject : null;
        so.ApplyModifiedPropertiesWithoutUndo();

        var fx = ensure(player, fxT);
        var so2 = new UnityEditor.SerializedObject(fx);
        so2.FindProperty("_settings").objectReferenceValue = settings;
        var sp = so2.FindProperty("_sortingConfig");
        if (sp != null) sp.objectReferenceValue = sortCfg;
        so2.ApplyModifiedPropertiesWithoutUndo();
        sb.Append(" wc+fx wired sortCfg=" + (sortCfg ? "ok" : "none"));

        // juice singletons present?
        var hsT = T("Flynn.Effects.HitStop");
        var hiT = T("Flynn.Effects.HitImpactFX");
        var managers = UnityEngine.GameObject.Find("MANAGERS");
        if (managers != null) {
            if (hsT != null && UnityEngine.Object.FindObjectOfType(hsT) == null) { managers.AddComponent(hsT); sb.Append(" +HitStop"); }
            if (hiT != null && UnityEngine.Object.FindObjectOfType(hiT) == null) { managers.AddComponent(hiT); sb.Append(" +HitImpactFX"); }
        } else sb.Append(" MANAGERS=NONE");

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        sb.Append(" scene=saved");
        return sb.ToString();
    """)
    receipt.append(f"wiring: {result}")

    # ── 4. final console check ────────────────────────────────────────────
    r = b.call("read_console", "get", types=["error"], count=10)
    data = (r.get("data") or {}).get("data") or r.get("data") or {}
    s = str(data)
    receipt.append("console: clean" if (not data or s in ("{}", "[]", "None")) else f"console: {s[:300]}")

    b.close()
    print("\n".join(receipt[:15]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
