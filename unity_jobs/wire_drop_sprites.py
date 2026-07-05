"""Wire 009-item-drops sheet: drop prefab sprites (single + pile) and
per-resource-type debris spray sprites on all nodes (scene + resource prefabs).
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from unity_bridge import UnityBridge

MUTATE = r"""
    var sb = new System.Text.StringBuilder();
    var sheet = "Assets/Flynn/Sprites/Environment/009-item-drops-and-quest-items-result-1.png";
    var byName = new System.Collections.Generic.Dictionary<string, Sprite>();
    foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(sheet))
        if (o is Sprite sp) byName[sp.name.Substring(sp.name.LastIndexOf('_') + 1)] = sp;
    if (byName.Count == 0) return "FAIL: sheet has no sub-sprites";
    sb.AppendLine("sheet sprites=" + byName.Count + " ppu=" + byName["5"].pixelsPerUnit);

    // itemKey -> (single, pile)
    var map = new System.Collections.Generic.Dictionary<string, string[]> {
        { "Assets/Flynn/Prefabs/Items/Wood_Drop.prefab",       new[]{"5","6"} },
        { "Assets/Flynn/Prefabs/Items/Stone_Drop.prefab",      new[]{"1","11"} },
        { "Assets/Flynn/Prefabs/Items/MetalScrap_Drop.prefab", new[]{"0","7"} },
        { "Assets/Flynn/Prefabs/Items/Biomass_Drop.prefab",    new[]{"2","9"} },
        { "Assets/Flynn/Prefabs/Items/EchoShard_Drop.prefab",  new[]{"4","8"} },
    };
    foreach (var kv in map) {
        var contents = UnityEditor.PrefabUtility.LoadPrefabContents(kv.Key);
        var sr = contents.GetComponent<SpriteRenderer>();
        var single = byName[kv.Value[0]];
        sr.sprite = single;
        sr.color = Color.white;

        // Normalize world size to ~0.35u wide regardless of source rect.
        float worldW = single.rect.width / single.pixelsPerUnit;
        float s = 0.35f / Mathf.Max(0.01f, worldW);
        contents.transform.localScale = new Vector3(s, s, 1f);

        var wi = contents.GetComponent<Flynn.World.WorldItem>();
        var wso = new UnityEditor.SerializedObject(wi);
        wso.FindProperty("_pileSprite").objectReferenceValue = byName[kv.Value[1]];
        wso.FindProperty("_pileThreshold").intValue = 3;
        wso.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.PrefabUtility.SaveAsPrefabAsset(contents, kv.Key);
        UnityEditor.PrefabUtility.UnloadPrefabContents(contents);
        sb.AppendLine(System.IO.Path.GetFileNameWithoutExtension(kv.Key)
            + " -> " + kv.Value[0] + "/pile " + kv.Value[1] + " scale=" + s.ToString("F2"));
    }

    // Debris spray per resource type: Wood->log chips(5), Stone->1, TechTrash->0, else leaves(2)
    System.Func<Flynn.Resources.ResourceType, Sprite[]> debrisFor = (kind) => {
        switch (kind) {
            case Flynn.Resources.ResourceType.Wood:      return new[]{ byName["5"], byName["3"] };
            case Flynn.Resources.ResourceType.Stone:     return new[]{ byName["1"] };
            case Flynn.Resources.ResourceType.TechTrash: return new[]{ byName["0"] };
            default:                                     return new[]{ byName["2"] };
        }
    };

    int wired = 0, added = 0;
    foreach (var node in UnityEngine.Object.FindObjectsOfType<Flynn.Resources.ResourceNode>(true)) {
        var burst = node.GetComponent<Flynn.Resources.HitDebrisBurst>();
        if (burst == null) { burst = node.gameObject.AddComponent<Flynn.Resources.HitDebrisBurst>(); added++; }
        var bso = new UnityEditor.SerializedObject(burst);
        var arrProp = bso.FindProperty("_debrisSprites");
        var sprites = debrisFor(node.ResourceKind);
        arrProp.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            arrProp.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        // small chips, cozy arc
        bso.FindProperty("_debrisScale").floatValue = 0.14f;
        bso.FindProperty("_countPerHit").intValue = 3;
        bso.ApplyModifiedPropertiesWithoutUndo();
        wired++;
    }
    sb.AppendLine("nodes debris wired=" + wired + " (component added to " + added + ")");

    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
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
        b.csharp("UnityEditor.AssetDatabase.Refresh(); return \"ok\";")
    except Exception:
        pass
    finally:
        try:
            b.close()
        except Exception:
            pass

    time.sleep(4)
    receipt = []
    b = connect_retry()
    try:
        for _ in range(6):
            try:
                b.call("manage_editor", "wait_for_compile")
                time.sleep(2)
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

        con = b.call("read_console", "get", types=["error"], count=8)
        d = (con.get("data") or {}).get("data") or {}
        ent = d.get("entries") if isinstance(d, dict) else d
        msgs = []
        for e in ent or []:
            m = (e.get("message") if isinstance(e, dict) else str(e)) or ""
            line = m.splitlines()[0][:140]
            if "error CS" in line and line not in msgs:
                msgs.append(line)
        receipt.append("compile errs: " + ("; ".join(msgs) if msgs else "none"))
    finally:
        b.close()

    print("\n".join(receipt[:20]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
