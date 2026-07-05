"""Fix magenta Shadow2D children + add elevation shadow tilemaps.

1. AssetDatabase.Refresh + wait_for_compile (Shadow2DManager.cs changed).
2. One C# batch: wire ProjectedShadow.mat into manager + all Shadow2D children,
   rebuild Ground_1..3 shadow silhouette tilemaps one sorting layer down, save.
3. read_console + screenshot receipt.
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from unity_bridge import UnityBridge

MUTATE = r"""
    var sb = new System.Text.StringBuilder();
    var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
        "Assets/Flynn/Materials/ProjectedShadow.mat");
    if (mat == null) return "FAIL: ProjectedShadow.mat not found";

    var mgrType = System.Type.GetType("Flynn.Shadow2D.Shadow2DManager, Flynn.Runtime");
    var mgr = UnityEngine.Object.FindObjectOfType(mgrType) as MonoBehaviour;
    if (mgr == null) return "FAIL: Shadow2DManager not in scene";
    var so = new UnityEditor.SerializedObject(mgr);
    so.FindProperty("_shadowMaterial").objectReferenceValue = mat;
    so.ApplyModifiedPropertiesWithoutUndo();
    sb.AppendLine("manager material wired");

    int healed = 0;
    foreach (var sr in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>(true))
        if (sr.gameObject.name == "Shadow2D") { sr.sharedMaterial = mat; healed++; }
    sb.AppendLine("healed children=" + healed);

    var grid = GameObject.Find("Ground Grid");
    if (grid == null) return sb + "FAIL: Ground Grid not found";
    for (int n = 1; n <= 3; n++)
    {
        var srcGO = GameObject.Find("Ground_" + n);
        if (srcGO == null) { sb.AppendLine("Ground_" + n + " missing, skip"); continue; }
        var src = srcGO.GetComponent<UnityEngine.Tilemaps.Tilemap>();

        var old = grid.transform.Find("Ground_" + n + "_Shadow");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

        var go = new GameObject("Ground_" + n + "_Shadow");
        go.transform.SetParent(grid.transform, false);
        go.transform.position = srcGO.transform.position + new Vector3(0.18f, -0.25f, 0f);

        var tm = go.AddComponent<UnityEngine.Tilemaps.Tilemap>();
        var tr = go.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();
        tr.sortingLayerName = "Level" + (n - 1);
        tr.sortingOrder = 5;
        tr.sharedMaterial = mat;

        src.CompressBounds();
        int copied = 0;
        foreach (var pos in src.cellBounds.allPositionsWithin)
        {
            var t = src.GetTile(pos);
            if (t != null) { tm.SetTile(pos, t); copied++; }
        }
        var mpb = new MaterialPropertyBlock();
        mpb.SetFloat("_Opacity", 0.6f);
        tr.SetPropertyBlock(mpb);
        sb.AppendLine("Ground_" + n + "_Shadow tiles=" + copied + " layer=Level" + (n - 1));
    }

    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
    sb.AppendLine("scene saved");
    return sb.ToString();
"""


def connect_retry(tries: int = 20, delay: float = 1.5) -> UnityBridge:
    last = None
    for _ in range(tries):
        try:
            b = UnityBridge().connect()
            if b.ping().get("success"):
                return b
        except Exception as e:  # editor reloading — retry
            last = e
            time.sleep(delay)
    raise RuntimeError(f"bridge unreachable after reload: {last}")


def main() -> int:
    receipt: list[str] = []

    b = connect_retry()
    try:
        b.csharp("UnityEditor.AssetDatabase.Refresh(); return \"refreshed\";")
    except Exception:
        pass  # connection may die on domain reload — expected
    finally:
        try:
            b.close()
        except Exception:
            pass

    time.sleep(3)
    b = connect_retry()
    try:
        try:
            b.call("manage_editor", "wait_for_compile")
        except Exception:
            b.close()
            time.sleep(3)
            b = connect_retry()

        r = b.csharp(MUTATE)
        receipt.append(str(r.get("result")))

        con = b.call("read_console", "get", types=["error"], count=10)
        data = (con.get("data") or {}).get("data") or con.get("data") or {}
        entries = data.get("entries") if isinstance(data, dict) else data
        msgs = []
        for e in entries or []:
            m = (e.get("message") if isinstance(e, dict) else str(e)) or ""
            line = m.splitlines()[0][:140]
            if line not in msgs:
                msgs.append(line)
        receipt.append("console errs: " + ("; ".join(msgs[:5]) if msgs else "none"))

        shot = b.call("manage_screenshot", "capture_game_view")
        receipt.append("shot: " + str((shot.get("data") or {}).get("path")))
    finally:
        b.close()

    print("\n".join(receipt[:20]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
