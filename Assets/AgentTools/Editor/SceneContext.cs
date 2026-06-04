using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AgentTools
{
    /// <summary>
    /// Agent-facing scene context provider. Compiled helper so the unity-executor
    /// can fetch screenshots and scene context with one-line execute_code calls
    /// instead of sending large code chunks every time. All methods are static and
    /// return compact agent-friendly strings (paths / lines / JSON-ish). Editor-only.
    ///
    /// Usage from execute_code (C# 6 safe, one line):
    ///   return AgentTools.SceneContext.CaptureViews("Wrench_Pickup");
    ///   return AgentTools.SceneContext.Describe("Wrench_Pickup");
    ///   return AgentTools.SceneContext.ListRenderables(30);
    /// </summary>
    public static class SceneContext
    {
        const string OutDirName = "AgentCaptures";

        // ---- capture ------------------------------------------------------

        /// <summary>
        /// Render high(60deg)/front/persp views of a target to PNGs.
        /// Returns newline-joined absolute PNG paths, or "ERR: ...".
        /// </summary>
        public static string CaptureViews(string targetName, int size = 1024)
        {
            var go = Find(targetName);
            if (go == null) return "ERR: target '" + targetName + "' not found in active scene" + Suggest(targetName);
            Bounds b;
            if (!TryBounds(go, out b)) return "ERR: target '" + targetName + "' has no Renderer bounds";

            var angles = new[]
            {
                new ViewAngle("high",  new Vector3(0f, 0.87f, 0.5f)), // ~60deg elevation
                new ViewAngle("front", new Vector3(0f, 0f, 1f)),
                new ViewAngle("persp", new Vector3(1f, 0.7f, 1f)),
            };

            var sb = new StringBuilder();
            foreach (var a in angles)
            {
                string p = RenderOne(b, a.Dir.normalized, size, targetName + "_" + a.Name);
                sb.Append(p).Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>Single custom-angle shot. elevationDeg up from horizon, yawDeg around Y.</summary>
        public static string Capture(string targetName, float elevationDeg, float yawDeg, int size = 1024)
        {
            var go = Find(targetName);
            if (go == null) return "ERR: target '" + targetName + "' not found" + Suggest(targetName);
            Bounds b;
            if (!TryBounds(go, out b)) return "ERR: target '" + targetName + "' has no Renderer bounds";

            float el = elevationDeg * Mathf.Deg2Rad;
            float ya = yawDeg * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(el) * Mathf.Sin(ya), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(ya));
            return RenderOne(b, dir.normalized, size, targetName + "_custom");
        }

/// <summary>
/// Render the named scene Camera to a PNG using its actual position, rotation, and FOV.
/// Temporarily sets targetTexture, calls Render(), then restores the previous targetTexture.
/// Returns the absolute PNG path, or "ERR: ...".
/// </summary>
public static string CaptureFromCamera(string cameraName, int size = 1024)
{
    Camera cam = null;
    foreach (var c in Object.FindObjectsOfType<Camera>(true))
    {
        if (string.Equals(c.gameObject.name, cameraName, System.StringComparison.OrdinalIgnoreCase))
        { cam = c; break; }
    }
    if (cam == null) return "ERR: no Camera named '" + cameraName + "' found in scene";

    var rt = new RenderTexture(size, size, 24);
    var prevRT = cam.targetTexture;
    string path = null;
    try
    {
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        path = Path.Combine(OutDir(), Sanitize(cameraName) + "_view.png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
    finally
    {
        cam.targetTexture = prevRT;
        rt.Release();
        Object.DestroyImmediate(rt);
    }
    return path;
}


        /// <summary>
        /// Render high/front/persp of a target composited into ONE horizontal-strip PNG.
        /// One image back instead of three -> far fewer vision tokens. Default 512/view.
        /// Returns the single PNG path, or "ERR: ...".
        /// </summary>
        public static string CaptureMontage(string targetName, int size = 512)
        {
            var go = Find(targetName);
            if (go == null) return "ERR: target '" + targetName + "' not found" + Suggest(targetName);
            Bounds b;
            if (!TryBounds(go, out b)) return "ERR: target '" + targetName + "' has no Renderer bounds";

            var dirs = new[]
            {
                new Vector3(0f, 0.87f, 0.5f).normalized, // high ~60deg
                new Vector3(0f, 0f, 1f).normalized,      // front
                new Vector3(1f, 0.7f, 1f).normalized,    // persp
            };
            var strip = new Texture2D(size * dirs.Length, size, TextureFormat.RGB24, false);
            for (int i = 0; i < dirs.Length; i++)
            {
                var px = RenderToPixels(b, dirs[i], size);
                strip.SetPixels(i * size, 0, size, size, px);
            }
            strip.Apply();
            string path = Path.Combine(OutDir(), Sanitize(targetName) + "_montage.png");
            File.WriteAllBytes(path, strip.EncodeToPNG());
            Object.DestroyImmediate(strip);
            return path;
        }

        static Color[] RenderToPixels(Bounds b, Vector3 dir, int size)
        {
            var camGO = new GameObject("agent_tempcam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.17f, 1f);
            float dist = Mathf.Max(b.size.magnitude, 0.1f) * 1.6f;
            cam.transform.position = b.center + dir * dist;
            cam.transform.LookAt(b.center);
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = dist * 4f + 100f;
            var rt = new RenderTexture(size, size, 24);
            Color[] px;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
                px = tex.GetPixels();
                RenderTexture.active = prev;
                Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGO);
            }
            return px;
        }

        static string RenderOne(Bounds b, Vector3 dir, int size, string label)
        {
            var camGO = new GameObject("agent_tempcam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.17f, 1f);

            float dist = Mathf.Max(b.size.magnitude, 0.1f) * 1.6f;
            cam.transform.position = b.center + dir * dist;
            cam.transform.LookAt(b.center);
            cam.orthographic = false;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = dist * 4f + 100f;

            var rt = new RenderTexture(size, size, 24);
            string path = null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                path = Path.Combine(OutDir(), Sanitize(label) + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGO);
            }
            return path;
        }

        // ---- context ------------------------------------------------------

        /// <summary>Compact description of one object: id, world pos, bounds, components, child count.</summary>
        public static string Describe(string targetName)
        {
            var go = Find(targetName);
            if (go == null) return "ERR: target '" + targetName + "' not found" + Suggest(targetName);
            var sb = new StringBuilder();
            sb.Append(go.name).Append(" id=").Append(go.GetInstanceID());
            sb.Append(" pos=").Append(V(go.transform.position));
            Bounds b;
            if (TryBounds(go, out b))
                sb.Append(" bounds.center=").Append(V(b.center)).Append(" size=").Append(V(b.size));
            sb.Append(" children=").Append(go.transform.childCount);
            var comps = go.GetComponents<Component>();
            sb.Append(" comps=[");
            for (int i = 0; i < comps.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(comps[i] == null ? "<missing>" : comps[i].GetType().Name);
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>List renderable root objects in the active scene: name | bounds size. Cap at max.</summary>
        public static string ListRenderables(int max = 50)
        {
            var sb = new StringBuilder();
            int n = 0;
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var rends = root.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) continue;
                Bounds b;
                TryBounds(root, out b);
                sb.Append(root.name).Append(" | size=").Append(V(b.size))
                  .Append(" | renderers=").Append(rends.Length).Append('\n');
                if (++n >= max) { sb.Append("...(capped at ").Append(max).Append(")\n"); break; }
            }
            if (n == 0) return "No renderable root objects in active scene.";
            return sb.ToString().TrimEnd('\n');
        }

        // ---- placement ----------------------------------------------------

        /// <summary>
        /// Drop a target straight down onto the nearest collider below it so its
        /// renderer bottom rests on that surface (kills floating). Ignores the
        /// target's own colliders. Returns old->new Y + what it landed on, or ERR.
        /// </summary>
        public static string GroundSnap(string targetName, float maxDrop = 50f)
        {
            var go = Find(targetName);
            if (go == null) return "ERR: target '" + targetName + "' not found" + Suggest(targetName);
            Bounds b;
            if (!TryBounds(go, out b)) return "ERR: target '" + targetName + "' has no Renderer bounds";

            var origin = new Vector3(b.center.x, b.center.y + maxDrop, b.center.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, maxDrop * 2f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit best = default(RaycastHit);
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsPartOf(hits[i].collider.transform, go.transform)) continue; // skip self
                if (!found || hits[i].distance < best.distance) { best = hits[i]; found = true; }
            }
            if (!found) return "ERR: no ground collider below '" + targetName + "' within " + maxDrop + "u (does the ground have a Collider?)";

            float bottomToPivot = go.transform.position.y - b.min.y; // >=0 distance pivot is above its bottom
            float oldY = go.transform.position.y;
            var p = go.transform.position;
            p.y = best.point.y + bottomToPivot;
            Undo.RecordObject(go.transform, "AgentTools GroundSnap");
            go.transform.position = p;
            Dirty(go);
            return "snap " + go.name + ": y " + F(oldY) + "->" + F(p.y) + " on '" + best.collider.gameObject.name + "'";
        }

        /// <summary>
        /// Edit-mode physics settle: temporarily give each target a Rigidbody (if it
        /// lacks one), run the physics sim for N steps so props fall and rest, then
        /// strip any Rigidbody this method added (transforms are kept). Targets need
        /// Colliders to interact. Returns per-object old->new position. Mutates scene.
        /// </summary>
        public static string Settle(string namesCsv, int steps = 120, float dt = 0.02f)
        {
            var gos = Resolve(namesCsv);
            if (gos.Count == 0) return "ERR: no targets resolved from '" + namesCsv + "'";

            var added = new List<Rigidbody>();
            var startPos = new List<Vector3>();
            var sb = new StringBuilder();
            foreach (var go in gos)
            {
                startPos.Add(go.transform.position);
                if (go.GetComponent<Collider>() == null && go.GetComponentInChildren<Collider>() == null)
                    sb.Append("WARN ").Append(go.name).Append(" has no Collider — will fall through.\n");
                var rb = go.GetComponent<Rigidbody>();
                if (rb == null) { rb = Undo.AddComponent<Rigidbody>(go); added.Add(rb); }
            }

            var prevMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                for (int i = 0; i < steps; i++) Physics.Simulate(dt);
            }
            finally
            {
                Physics.simulationMode = prevMode;
                foreach (var rb in added) if (rb != null) Object.DestroyImmediate(rb);
            }

            for (int i = 0; i < gos.Count; i++)
            {
                Dirty(gos[i]);
                sb.Append("settle ").Append(gos[i].name).Append(": ")
                  .Append(V(startPos[i])).Append("->").Append(V(gos[i].transform.position)).Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>
        /// Report overlapping pairs among targets (csv) or all renderable roots.
        /// Per pair: per-axis overlap extents (how deep they interpenetrate).
        /// </summary>
        public static string Overlaps(string namesCsv = null)
        {
            List<GameObject> gos;
            if (string.IsNullOrEmpty(namesCsv))
            {
                gos = new List<GameObject>();
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    if (root.GetComponentsInChildren<Renderer>().Length > 0) gos.Add(root);
            }
            else gos = Resolve(namesCsv);
            if (gos.Count < 2) return "Need 2+ renderable targets to check overlap.";

            var sb = new StringBuilder();
            int pairs = 0;
            for (int i = 0; i < gos.Count; i++)
            {
                Bounds bi; if (!TryBounds(gos[i], out bi)) continue;
                for (int j = i + 1; j < gos.Count; j++)
                {
                    Bounds bj; if (!TryBounds(gos[j], out bj)) continue;
                    if (!bi.Intersects(bj)) continue;
                    float ox = Overlap1D(bi.min.x, bi.max.x, bj.min.x, bj.max.x);
                    float oy = Overlap1D(bi.min.y, bi.max.y, bj.min.y, bj.max.y);
                    float oz = Overlap1D(bi.min.z, bi.max.z, bj.min.z, bj.max.z);
                    sb.Append(gos[i].name).Append(" x ").Append(gos[j].name)
                      .Append(" overlap=(").Append(F(ox)).Append(',').Append(F(oy)).Append(',').Append(F(oz)).Append(")\n");
                    pairs++;
                }
            }
            if (pairs == 0) return "No overlaps among " + gos.Count + " targets.";
            return sb.ToString().TrimEnd('\n') + "\n" + pairs + " overlapping pair(s).";
        }

        static float Overlap1D(float aMin, float aMax, float bMin, float bMax)
        {
            float o = Mathf.Min(aMax, bMax) - Mathf.Max(aMin, bMin);
            return o > 0f ? o : 0f;
        }

        static List<GameObject> Resolve(string namesCsv)
        {
            var outp = new List<GameObject>();
            if (string.IsNullOrEmpty(namesCsv)) return outp;
            foreach (var raw in namesCsv.Split(','))
            {
                var n = raw.Trim();
                if (n.Length == 0) continue;
                var go = Find(n);
                if (go != null && !outp.Contains(go)) outp.Add(go);
            }
            return outp;
        }

        static bool IsPartOf(Transform t, Transform root)
        {
            while (t != null) { if (t == root) return true; t = t.parent; }
            return false;
        }

        static void Dirty(GameObject go)
        {
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        static string F(float v) { return v.ToString("0.###"); }

        // ---- helpers ------------------------------------------------------

        static GameObject Find(string name)
        {
            // exact match first, then case-insensitive fallback. active-scene, incl. inactive.
            var go = FindBy(name, false);
            if (go != null) return go;
            return FindBy(name, true);
        }

        static GameObject FindBy(string name, bool ignoreCase)
        {
            var cmp = ignoreCase ? System.StringComparison.OrdinalIgnoreCase : System.StringComparison.Ordinal;
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (string.Equals(root.name, name, cmp)) return root;
                var t = FindInChildren(root.transform, name, cmp);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        /// <summary>Up to 5 object names containing the query (case-insensitive), for "did you mean" hints.</summary>
        static string Suggest(string name)
        {
            var hits = new List<string>();
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                CollectMatches(root.transform, name, hits);
            if (hits.Count == 0) return "";
            if (hits.Count > 5) hits = hits.GetRange(0, 5);
            return " — did you mean: " + string.Join(", ", hits.ToArray());
        }

        static void CollectMatches(Transform t, string q, List<string> hits)
        {
            if (hits.Count >= 5) return;
            if (t.name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0) hits.Add(t.name);
            for (int i = 0; i < t.childCount; i++) CollectMatches(t.GetChild(i), q, hits);
        }

        static Transform FindInChildren(Transform t, string name, System.StringComparison cmp)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (string.Equals(c.name, name, cmp)) return c;
                var r = FindInChildren(c, name, cmp);
                if (r != null) return r;
            }
            return null;
        }

        static bool TryBounds(GameObject go, out Bounds b)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { b = new Bounds(go.transform.position, Vector3.zero); return false; }
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return true;
        }

        static string OutDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), OutDirName);
            Directory.CreateDirectory(dir);
            return dir;
        }

        static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        static string V(Vector3 v)
        {
            return "(" + v.x.ToString("0.##") + "," + v.y.ToString("0.##") + "," + v.z.ToString("0.##") + ")";
        }

        struct ViewAngle
        {
            public readonly string Name;
            public readonly Vector3 Dir;
            public ViewAngle(string n, Vector3 d) { Name = n; Dir = d; }
        }
    }
}
