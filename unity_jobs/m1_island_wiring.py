"""M1+M2 island-1 loop wiring: CrashedPod/PodBurner/RepairPoint/FeedPoint,
DemoFlow director, GateKeeper trade NPC, gate barrier, metal nodes, end zone,
ExhaustedDebuff on player, llmEnabled off. One flush; compact receipt.
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from unity_bridge import UnityBridge

MUTATE = r"""
    var sb = new System.Text.StringBuilder();
    var BF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    // ── assets ────────────────────────────────────────────────────────
    var wood  = UnityEditor.AssetDatabase.LoadAssetAtPath<Flynn.Player.ItemDefinition>("Assets/Flynn/Configs/Items/Wood_Item.asset");
    var stone = UnityEditor.AssetDatabase.LoadAssetAtPath<Flynn.Player.ItemDefinition>("Assets/Flynn/Configs/Items/Stone_Item.asset");
    var metal = UnityEditor.AssetDatabase.LoadAssetAtPath<Flynn.Player.ItemDefinition>("Assets/Flynn/Configs/Items/MetalScrap_Item.asset");
    var anchor = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Flynn/Configs/Player/PlayerAnchor.asset");
    var metalPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Flynn/Prefabs/Resources/Metal_Scrap.prefab");
    if (wood == null || stone == null || metal == null || anchor == null) return "FAIL: item/anchor assets missing";

    System.Func<string, Sprite> findSprite = (needle) => {
        foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:Sprite " + needle, new[]{"Assets/Flynn"})) {
            var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
            var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s != null) return s;
        }
        return null;
    };
    Sprite square = null;
    foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[]{"Assets/Flynn/Art"})) {
        var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
        if (s != null) { square = s; break; }
    }

    // ── marker positions ──────────────────────────────────────────────
    var markers = GameObject.Find("MapMarkers");
    Vector3 podPos = new Vector3(-2f, 5.25f, 0f), gatePos = new Vector3(1.5f, 4.2f, 0f);
    if (markers != null) {
        foreach (Transform m in markers.transform) {
            if (m.name == "Pod") podPos = m.position;
            if (m.name.ToLower().Contains("gate")) gatePos = m.position;
        }
    }
    var gateGO = GameObject.Find("Gate to island 2");
    if (gateGO != null) gatePos = gateGO.transform.position;

    // ── helpers ───────────────────────────────────────────────────────
    System.Func<string, Vector3, Sprite, Color, float, GameObject> makeSpriteGO =
        (name, pos, sprite, tint, order) => {
            var go = new GameObject(name);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : square;
            sr.color = tint;
            sr.sortingLayerName = "Level0";
            sr.sortingOrder = (int)order;
            return go;
        };

    var anchorField = typeof(Flynn.Player.Interaction.Interactable).GetField("_playerAnchor", BF);
    var radiusField = typeof(Flynn.Player.Interaction.Interactable).GetField("_interactionRadius", BF);
    var verbField   = typeof(Flynn.Player.Interaction.Interactable).GetField("_promptVerb", BF);
    var evtField    = typeof(Flynn.Player.Interaction.Interactable).GetField("_onInteract", BF);

    System.Func<GameObject, string, float, Flynn.Player.Interaction.Interactable> addInteract =
        (go, verb, radius) => {
            var ia = go.AddComponent<Flynn.Player.Interaction.Interactable>();
            anchorField.SetValue(ia, anchor);
            radiusField.SetValue(ia, radius);
            verbField.SetValue(ia, verb);
            return ia;
        };

    // ── cleanup previous run ──────────────────────────────────────────
    foreach (var n in new[]{"CrashedPod","DemoFlow","GateKeeper","Island2GateBarrier","DemoEndZone",
                            "MetalNode_1","MetalNode_2","MetalNode_3","MetalNode_4"}) {
        var old = GameObject.Find(n);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
    }

    // ── DemoFlow (director + end screen) ──────────────────────────────
    var managers = GameObject.Find("MANAGERS");
    var flowGO = new GameObject("DemoFlow");
    if (managers != null) flowGO.transform.SetParent(managers.transform, false);
    var director = flowGO.AddComponent<Flynn.Demo.DemoFlowDirector>();
    flowGO.AddComponent<Flynn.Demo.DemoEndScreen>();

    var ion = GameObject.Find("ion_TransmitterStation");
    var scanUi = UnityEngine.Object.FindObjectOfType<Flynn.Tutorial.ScanUIController>(true);
    var dso = new UnityEditor.SerializedObject(director);
    if (ion != null) dso.FindProperty("_stirTarget").objectReferenceValue = ion.transform;
    if (scanUi != null) dso.FindProperty("_scanUi").objectReferenceValue = scanUi;

    // ── CrashedPod + PodPower ─────────────────────────────────────────
    var podSprite = findSprite("pod");
    var podGO = makeSpriteGO("CrashedPod", podPos, podSprite, new Color(0.5f,0.53f,0.6f,1f), 2f);
    var podCol = podGO.AddComponent<CircleCollider2D>();
    podCol.radius = 0.45f;
    var pod = podGO.AddComponent<Flynn.Pod.PodPower>();
    var pso = new UnityEditor.SerializedObject(pod);
    pso.FindProperty("_playerAnchor").objectReferenceValue = anchor;
    pso.FindProperty("_renderer").objectReferenceValue = podGO.GetComponent<SpriteRenderer>();
    pso.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.Events.UnityEventTools.AddPersistentListener(pod.onStabilised,
        new UnityEngine.Events.UnityAction(director.OnPodStabilised));

    // ── PodBurner + repair/feed points ────────────────────────────────
    var burnerSprite = findSprite("burner") ?? findSprite("furnace") ?? findSprite("stove");
    var burnerGO = makeSpriteGO("PodBurner", podPos + new Vector3(0.9f, -0.35f, 0f),
        burnerSprite, new Color(0.45f,0.4f,0.4f,1f), 3f);
    burnerGO.transform.SetParent(podGO.transform, true);
    var bcol = burnerGO.AddComponent<BoxCollider2D>();
    bcol.isTrigger = true; bcol.size = new Vector2(0.6f, 0.6f);

    var burner = burnerGO.AddComponent<Flynn.Pod.BurnerStation>();
    var bso = new UnityEditor.SerializedObject(burner);
    bso.FindProperty("_pod").objectReferenceValue = pod;
    bso.FindProperty("_acceptedItem").objectReferenceValue = wood;
    bso.ApplyModifiedPropertiesWithoutUndo();

    var rep = burnerGO.AddComponent<Flynn.Pod.RepairableStructure>();
    var rso = new UnityEditor.SerializedObject(rep);
    rso.FindProperty("_renderer").objectReferenceValue = burnerGO.GetComponent<SpriteRenderer>();
    var stages = rso.FindProperty("_stages");
    stages.arraySize = 1;
    var st0 = stages.GetArrayElementAtIndex(0);
    var costs = st0.FindPropertyRelative("costs");
    costs.arraySize = 2;
    costs.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = wood;
    costs.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue = 3;
    costs.GetArrayElementAtIndex(1).FindPropertyRelative("item").objectReferenceValue = stone;
    costs.GetArrayElementAtIndex(1).FindPropertyRelative("count").intValue = 2;
    rso.FindProperty("_stageToast").stringValue = "Burner repaired. It wants wood.";
    rso.FindProperty("_finalToast").stringValue = "Burner repaired. It wants wood.";
    rso.ApplyModifiedPropertiesWithoutUndo();

    var repairPoint = new GameObject("RepairPoint");
    repairPoint.transform.SetParent(burnerGO.transform, false);
    var rpc = repairPoint.AddComponent<BoxCollider2D>(); rpc.isTrigger = true; rpc.size = new Vector2(0.7f, 0.7f);
    var repIa = addInteract(repairPoint, "Repair", 1.6f);
    UnityEditor.Events.UnityEventTools.AddPersistentListener(
        (UnityEngine.Events.UnityEvent)evtField.GetValue(repIa),
        new UnityEngine.Events.UnityAction(rep.TryAdvanceStage));

    var feedPoint = new GameObject("FeedPoint");
    feedPoint.transform.SetParent(burnerGO.transform, false);
    var fpc = feedPoint.AddComponent<BoxCollider2D>(); fpc.isTrigger = true; fpc.size = new Vector2(0.7f, 0.7f);
    var feedIa = addInteract(feedPoint, "Feed Wood", 1.6f);
    UnityEditor.Events.UnityEventTools.AddPersistentListener(
        (UnityEngine.Events.UnityEvent)evtField.GetValue(feedIa),
        new UnityEngine.Events.UnityAction(burner.TryBurn));
    feedPoint.SetActive(false);

    UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(rep.onFullyRepaired, repairPoint.SetActive, false);
    UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(rep.onFullyRepaired, feedPoint.SetActive, true);
    UnityEditor.Events.UnityEventTools.AddPersistentListener(rep.onFullyRepaired,
        new UnityEngine.Events.UnityAction(director.OnBurnerRepaired));

    // ── Gate barrier + GateKeeper + end zone ──────────────────────────
    var barrier = makeSpriteGO("Island2GateBarrier", gatePos, findSprite("gate") ?? findSprite("fence"),
        new Color(0.35f,0.3f,0.28f,1f), 4f);
    var barCol = barrier.AddComponent<BoxCollider2D>();
    barCol.size = new Vector2(1.2f, 0.9f);
    dso.FindProperty("_gateBarrier").objectReferenceValue = barrier;
    dso.ApplyModifiedPropertiesWithoutUndo();

    var keeperSprite = findSprite("old") ?? findSprite("npc") ?? findSprite("character");
    var keeper = makeSpriteGO("GateKeeper", gatePos + new Vector3(-0.9f, 0.35f, 0f), keeperSprite, Color.white, 5f);
    var kcol = keeper.AddComponent<CircleCollider2D>(); kcol.isTrigger = true; kcol.radius = 0.4f;
    var npc = keeper.AddComponent<Flynn.Demo.ScriptedTradeNpc>();
    var nso = new UnityEditor.SerializedObject(npc);
    nso.FindProperty("_requiredItem").objectReferenceValue = metal;
    nso.FindProperty("_requiredCount").intValue = 3;
    nso.ApplyModifiedPropertiesWithoutUndo();
    var talkIa = addInteract(keeper, "Talk", 1.8f);
    UnityEditor.Events.UnityEventTools.AddPersistentListener(
        (UnityEngine.Events.UnityEvent)evtField.GetValue(talkIa),
        new UnityEngine.Events.UnityAction(npc.Talk));
    UnityEditor.Events.UnityEventTools.AddPersistentListener(npc.onFirstTalk,
        new UnityEngine.Events.UnityAction(director.OnFirstTalk));
    UnityEditor.Events.UnityEventTools.AddPersistentListener(npc.onTradeCompleted,
        new UnityEngine.Events.UnityAction(director.OnTradeCompleted));

    var endZone = new GameObject("DemoEndZone");
    endZone.transform.position = gatePos + new Vector3(0.9f, -0.3f, 0f);
    var trig = endZone.AddComponent<Flynn.Demo.DemoEndTrigger>();
    var tso = new UnityEditor.SerializedObject(trig);
    tso.FindProperty("_playerAnchor").objectReferenceValue = anchor;
    tso.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.Events.UnityEventTools.AddPersistentListener(trig.onEntered,
        new UnityEngine.Events.UnityAction(director.OnReachedGate));

    // ── metal nodes near existing stone nodes ─────────────────────────
    int placed = 0;
    if (metalPrefab != null) {
        var offsets = new[]{ new Vector3(0.8f,-0.4f,0), new Vector3(-0.7f,-0.6f,0),
                             new Vector3(0.9f,0.5f,0), new Vector3(-0.8f,0.6f,0) };
        foreach (var node in UnityEngine.Object.FindObjectsOfType<Flynn.Resources.ResourceNode>()) {
            if (placed >= 4) break;
            var srCheck = node.GetComponentInChildren<SpriteRenderer>();
            if (!node.name.ToLower().Contains("stone") && !node.name.ToLower().Contains("rock")) continue;
            var inst = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(metalPrefab);
            inst.name = "MetalNode_" + (placed + 1);
            inst.transform.position = node.transform.position + offsets[placed];
            placed++;
        }
    }

    // ── player debuff + llm off ───────────────────────────────────────
    var player = GameObject.Find("Player");
    if (player != null && player.GetComponent<Flynn.Player.ExhaustedDebuff>() == null)
        player.AddComponent<Flynn.Player.ExhaustedDebuff>();

    var llmMgr = UnityEngine.Object.FindObjectOfType<Flynn.Npc.SceneLlmManager>(true);
    if (llmMgr != null) {
        var lso = new UnityEditor.SerializedObject(llmMgr);
        var lp = lso.FindProperty("llmEnabled");
        if (lp != null) {
            if (lp.propertyType == UnityEditor.SerializedPropertyType.Boolean) lp.boolValue = false;
            else lp.intValue = 0;
            lso.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine("llmEnabled -> off");
        }
    }

    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
    sb.AppendLine("pod@" + podPos.ToString("F2") + " gate@" + gatePos.ToString("F2"));
    sb.AppendLine("podSprite=" + (podSprite ? podSprite.name : "fallback")
        + " keeperSprite=" + (keeperSprite ? keeperSprite.name : "fallback"));
    sb.AppendLine("metal nodes placed=" + placed);
    sb.AppendLine("wired: repair->feed swap, pod.onStabilised, npc events, end zone, saved");
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
            line = m.splitlines()[0][:160]
            if line not in msgs:
                msgs.append(line)
        receipt.append("console errs: " + ("; ".join(msgs[:8]) if msgs else "none"))

        shot = b.call("manage_screenshot", "capture_game_view")
        receipt.append("shot: " + str((shot.get("data") or {}).get("path")))
    finally:
        b.close()

    print("\n".join(receipt[:25]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
