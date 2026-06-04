using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AgentTools
{
    /// <summary>
    /// Data-driven, idempotent scene builder. The agent ships a COMPACT SPEC (text)
    /// instead of a large imperative execute_code script — the "how" lives here,
    /// compiled. Re-running Build wipes + rebuilds the root, so iterating costs one
    /// tiny spec, not a full resend. Editor-only.
    ///
    /// One-line usage from execute_code:
    ///   return AgentTools.SceneBuilder.Build(@"
    ///     root SANDBOX_PLAYGROUND
    ///     ground Ground 7.5,6.5 32,26 grey-green
    ///     tile  Plaza 7.5,6.5 7,5 grey
    ///     label Plaza 7.5,6.5 'Plaza (Spawn)'
    ///     tree  HarvestGrove -4,14 ; -1,15 ; -3,16
    ///     soil  FarmPlot 7.5,14.5 3
    ///     rock  Mining 17,14 ; 18,15 ; 17.5,16
    ///     crate Push 6,-2 ; 8,-1 ; 10,-2
    ///     prim  PowerLab cube EnergyNode 17.5,6.5 1,1,1 cyan
    ///   ");
    ///
    /// Ground top sits at Y=0; objects are placed so their base rests on Y=0.
    /// Tints: hex (#RRGGBB) or names: grey, grey-green, green, dgreen, brown, orange,
    /// cyan, purple, yellow, teal, blue, red, white, dark. Labels author at rotation
    /// [0,0,0] (reads correctly for the -Z Main Camera). Lines starting with # ignored.
    /// </summary>
    public static class SceneBuilder
    {
        static GameObject _root;
        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Transform> _zones = new Dictionary<string, Transform>();

        public static string Build(string spec)
        {
            if (string.IsNullOrEmpty(spec)) return "ERR: empty spec";
            _root = null; _mats.Clear(); _zones.Clear();
            var counts = new Dictionary<string, int>();
            var errors = new List<string>();
            int line = 0;

            foreach (var rawLine in spec.Replace("\r", "").Split('\n'))
            {
                line++;
                var s = rawLine.Trim();
                if (s.Length == 0 || s.StartsWith("#")) continue;
                try
                {
                    string verb = NextToken(ref s).ToLowerInvariant();
                    switch (verb)
                    {
                        case "root":   DoRoot(NextToken(ref s)); break;
                        case "ground": DoGround(s); break;
                        case "platform": DoPlatform(s); break;
                        case "ramp":   DoRamp(s); break;
                        case "tile":   DoTile(s); break;
                        case "label":  DoLabel(s); break;
                        case "prim":   DoPrim(s); break;
                        case "tree":   DoRepeat(s, "tree", Tree); break;
                        case "crate":  DoRepeat(s, "crate", Crate); break;
                        case "rock":   DoRepeat(s, "rock", Rock); break;
                        case "soil":   DoSoil(s); break;
                        default: errors.Add("L" + line + " unknown verb '" + verb + "'"); continue;
                    }
                    Bump(counts, verb);
                }
                catch (System.Exception e) { errors.Add("L" + line + " '" + s + "' -> " + e.Message); }
            }

            if (_root != null)
            {
                EditorUtility.SetDirty(_root);
                EditorSceneManager.MarkSceneDirty(_root.scene);
            }

            var sb = new StringBuilder();
            sb.Append(_root == null ? "(no root)" : "root " + _root.name);
            sb.Append(": zones=").Append(_zones.Count);
            sb.Append(" objects=").Append(_root == null ? 0 : CountDescendants(_root.transform));
            sb.Append(" directives=[");
            bool first = true;
            foreach (var kv in counts) { if (!first) sb.Append(' '); sb.Append(kv.Key).Append('×').Append(kv.Value); first = false; }
            sb.Append(']');
            if (errors.Count > 0) { sb.Append("\nERRORS(").Append(errors.Count).Append("):\n"); sb.Append(string.Join("\n", errors)); }
            return sb.ToString();
        }

        // ---- verbs --------------------------------------------------------

        static void DoRoot(string name)
        {
            if (string.IsNullOrEmpty(name)) name = "SANDBOX_PLAYGROUND";
            var existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing); // idempotent rebuild
            _root = new GameObject(name);
            _root.transform.position = Vector3.zero;
        }

        static void DoGround(string s)
        {
            string name = NextToken(ref s);
            Vector2 c = XZ(NextToken(ref s));
            Vector2 sz = WD(NextToken(ref s));
            var col = Col(NextToken(ref s));
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = new Vector3(c.x, -0.5f, c.y);       // top at Y=0
            go.transform.localScale = new Vector3(sz.x, 1f, sz.y);
            Paint(go, col);
            Parent(go, null);
        }

        // platform NAME cx,cz W,D y tint  -> a solid block whose TOP surface sits at height y
        static void DoPlatform(string s)
        {
            string name = NextToken(ref s);
            Vector2 c = XZ(NextToken(ref s));
            Vector2 sz = WD(NextToken(ref s));
            float y = P(NextToken(ref s));
            var col = Col(NextToken(ref s));
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = new Vector3(c.x, y - 0.5f, c.y); // 1-thick, top at y
            go.transform.localScale = new Vector3(sz.x, 1f, sz.y);
            Paint(go, col);
            Parent(go, null);
        }

        // ramp NAME cx,cz W,D y0,y1 tint  -> a walkable slope from height y0 to y1 across D (Z run)
        static void DoRamp(string s)
        {
            string name = NextToken(ref s);
            Vector2 c = XZ(NextToken(ref s));
            Vector2 sz = WD(NextToken(ref s)); // x = width, y = Z run
            var ys = NextToken(ref s).Split(',');
            float y0 = P(ys[0]);
            float y1 = ys.Length > 1 ? P(ys[1]) : y0;
            var col = Col(NextToken(ref s));
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            float run = Mathf.Max(sz.y, 0.01f);
            float rise = y1 - y0;
            float len = Mathf.Sqrt(run * run + rise * rise);
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            go.transform.position = new Vector3(c.x, (y0 + y1) * 0.5f, c.y);
            go.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
            go.transform.localScale = new Vector3(sz.x, 0.3f, len);
            Paint(go, col);
            Parent(go, null);
        }

        static void DoTile(string s)
        {
            string zone = NextToken(ref s);
            Vector2 c = XZ(NextToken(ref s));
            Vector2 sz = WD(NextToken(ref s));
            var col = Col(NextToken(ref s));
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());       // markers shouldn't block
            go.name = "Tile_" + zone;
            go.transform.position = new Vector3(c.x, 0.03f, c.y);
            go.transform.localScale = new Vector3(sz.x, 0.05f, sz.y);
            Paint(go, col);
            Parent(go, zone);
        }

        static void DoLabel(string s)
        {
            string zone = NextToken(ref s);
            Vector2 c = XZ(NextToken(ref s));
            string text = Quoted(ref s);
            float y = 2.6f;
            string ytok = NextToken(ref s);
            if (ytok.Length > 0) float.TryParse(ytok, NumberStyles.Float, CultureInfo.InvariantCulture, out y);

            var go = new GameObject("Label_" + zone);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.08f;
            tm.fontSize = 64;
            tm.color = Color.white;
            go.transform.position = new Vector3(c.x, y, c.y);
            go.transform.rotation = Quaternion.identity;                // [0,0,0] reads for -Z camera
            Parent(go, zone);
        }

        static void DoPrim(string s)
        {
            string zone = NextToken(ref s);
            var shape = ParseShape(NextToken(ref s));
            string name = NextToken(ref s);
            Vector2 c = XZ(NextToken(ref s));
            Vector3 sc = XYZ(NextToken(ref s));
            var col = Col(NextToken(ref s));
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.localScale = sc;
            go.transform.position = new Vector3(c.x, sc.y * 0.5f, c.y);  // base on ground
            Paint(go, col);
            Parent(go, zone);
        }

        static void DoSoil(string s)
        {
            string zone = NextToken(ref s);
            Vector2 c = XZ(NextToken(ref s));
            int n = 3;
            string ntok = NextToken(ref s);
            if (ntok.Length > 0) int.TryParse(ntok, out n);
            n = Mathf.Clamp(n, 1, 6);
            float off = (n - 1) * 0.5f;
            for (int ix = 0; ix < n; ix++)
                for (int iz = 0; iz < n; iz++)
                {
                    var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cell.name = "Soil_" + ix + "_" + iz;
                    cell.transform.localScale = new Vector3(0.8f, 0.1f, 0.8f);
                    cell.transform.position = new Vector3(c.x + (ix - off), 0.05f, c.y + (iz - off));
                    Paint(cell, Col("brown"));
                    Parent(cell, zone);
                    if (ix == n / 2 && iz == n / 2) // center sprout
                    {
                        var sprout = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        sprout.name = "Sprout";
                        sprout.transform.localScale = new Vector3(0.2f, 0.3f, 0.2f);
                        sprout.transform.position = new Vector3(c.x + (ix - off), 0.25f, c.y + (iz - off));
                        Paint(sprout, Col("green"));
                        Parent(sprout, zone);
                    }
                }
        }

        // ---- presets ------------------------------------------------------

        static void Tree(string zone, Vector2 p, int i)
        {
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Tree_" + i + "_trunk";
            trunk.transform.localScale = new Vector3(0.3f, 0.6f, 0.3f);
            trunk.transform.position = new Vector3(p.x, 0.6f, p.y);
            Paint(trunk, Col("brown"));
            Parent(trunk, zone);
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Tree_" + i + "_canopy";
            canopy.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            canopy.transform.position = new Vector3(p.x, 1.65f, p.y);
            Paint(canopy, Col("dgreen"));
            Parent(canopy, zone);
        }

        static void Crate(string zone, Vector2 p, int i)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Crate_" + i;
            go.transform.position = new Vector3(p.x, 0.5f, p.y);
            Paint(go, Col("yellow"));
            Parent(go, zone);
        }

        static void Rock(string zone, Vector2 p, int i)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Rock_" + i;
            float sc = 0.8f + (i % 3) * 0.2f;
            go.transform.localScale = new Vector3(sc, sc * 0.8f, sc);
            go.transform.position = new Vector3(p.x, sc * 0.4f, p.y);
            go.transform.rotation = Quaternion.Euler(0, i * 25f, 0);
            Paint(go, Col("grey"));
            Parent(go, zone);
        }

        // ---- parsing helpers ----------------------------------------------

        static void DoRepeat(string s, string label, System.Action<string, Vector2, int> spawn)
        {
            string zone = NextToken(ref s);
            var pts = s.Split(';');
            int i = 0;
            foreach (var raw in pts)
            {
                var t = raw.Trim();
                if (t.Length == 0) continue;
                spawn(zone, XZ(t), i++);
            }
        }

        static string NextToken(ref string s)
        {
            s = s.TrimStart();
            if (s.Length == 0) return "";
            int sp = s.IndexOf(' ');
            if (sp < 0) { var all = s; s = ""; return all; }
            var tok = s.Substring(0, sp);
            s = s.Substring(sp + 1);
            return tok;
        }

        static string Quoted(ref string s)
        {
            s = s.TrimStart();
            char q = (s.Length > 0 && (s[0] == '\'' || s[0] == '"')) ? s[0] : '\0';
            if (q == '\0') return NextToken(ref s);
            int end = s.IndexOf(q, 1);
            if (end < 0) { var all = s.Substring(1); s = ""; return all; }
            var text = s.Substring(1, end - 1);
            s = s.Substring(end + 1).TrimStart();
            return text;
        }

        static Vector2 XZ(string t)
        {
            var p = t.Split(',');
            return new Vector2(P(p[0]), P(p.Length > 1 ? p[1] : "0"));
        }

        static Vector2 WD(string t)
        {
            var p = t.Split(',');
            return new Vector2(P(p[0]), P(p.Length > 1 ? p[1] : p[0]));
        }

        static Vector3 XYZ(string t)
        {
            var p = t.Split(',');
            return new Vector3(P(p[0]), P(p.Length > 1 ? p[1] : p[0]), P(p.Length > 2 ? p[2] : p[0]));
        }

        static float P(string v)
        {
            float f;
            float.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);
            return f;
        }

        static PrimitiveType ParseShape(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "sphere":   return PrimitiveType.Sphere;
                case "cylinder": return PrimitiveType.Cylinder;
                case "capsule":  return PrimitiveType.Capsule;
                case "quad":     return PrimitiveType.Quad;
                case "plane":    return PrimitiveType.Plane;
                default:         return PrimitiveType.Cube;
            }
        }

        static Color Col(string s)
        {
            if (string.IsNullOrEmpty(s)) return new Color(0.7f, 0.7f, 0.7f);
            Color c;
            if (s.StartsWith("#") && ColorUtility.TryParseHtmlString(s, out c)) return c;
            switch (s.ToLowerInvariant())
            {
                case "grey": case "gray": return new Color(0.6f, 0.6f, 0.6f);
                case "grey-green": case "earth": return new Color(0.42f, 0.5f, 0.36f);
                case "green":  return new Color(0.35f, 0.7f, 0.3f);
                case "dgreen": return new Color(0.18f, 0.45f, 0.18f);
                case "brown":  return new Color(0.45f, 0.32f, 0.18f);
                case "orange": return new Color(0.9f, 0.55f, 0.2f);
                case "cyan":   return new Color(0.3f, 0.8f, 0.85f);
                case "purple": return new Color(0.55f, 0.35f, 0.75f);
                case "yellow": return new Color(0.9f, 0.8f, 0.25f);
                case "teal":   return new Color(0.2f, 0.6f, 0.6f);
                case "blue":   return new Color(0.25f, 0.4f, 0.8f);
                case "red":    return new Color(0.8f, 0.3f, 0.3f);
                case "white":  return Color.white;
                case "dark":   return new Color(0.22f, 0.22f, 0.24f);
                default:       return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        static void Paint(GameObject go, Color c)
        {
            string key = c.r + "_" + c.g + "_" + c.b;
            Material m;
            if (!_mats.TryGetValue(key, out m))
            {
                m = new Material(Shader.Find("Standard"));
                m.color = c;
                _mats[key] = m;
            }
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = m;
        }

        static void Parent(GameObject go, string zone)
        {
            if (_root == null) DoRoot("SANDBOX_PLAYGROUND");
            if (string.IsNullOrEmpty(zone)) { go.transform.SetParent(_root.transform, true); return; }
            Transform z;
            if (!_zones.TryGetValue(zone, out z))
            {
                var zgo = new GameObject("Group_" + zone);
                zgo.transform.SetParent(_root.transform, false);
                z = zgo.transform;
                _zones[zone] = z;
            }
            go.transform.SetParent(z, true);
        }

        static void Bump(Dictionary<string, int> d, string k)
        {
            int v; d.TryGetValue(k, out v); d[k] = v + 1;
        }

        static int CountDescendants(Transform t)
        {
            int n = t.childCount;
            for (int i = 0; i < t.childCount; i++) n += CountDescendants(t.GetChild(i));
            return n;
        }
    }
}
