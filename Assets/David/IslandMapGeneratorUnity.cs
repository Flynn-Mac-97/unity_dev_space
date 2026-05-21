using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Unity-friendly island map generator.
// Call GenerateAndPrint() from the inspector or Start() to print the 2D array.
namespace David
{
    sealed class SeededRng
    {
        private uint _s;
        public SeededRng(int seed) => _s = (uint)seed;
        public double Next() { _s = unchecked(_s * 1664525u + 1013904223u); return _s / 4294967296.0; }
        public int Int(int a, int b) => (int)Math.Floor(Next() * (b - a)) + a;
        public double Float(double a, double b) => Next() * (b - a) + a;
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Int(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    public enum Terrain : byte { Sky = 0, Land = 1, Mountain = 2, Lake = 3, Forest = 4 }

    static class MapGen
    {
        static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
        static int    Clamp(int v,    int lo,    int hi)    => Math.Max(lo, Math.Min(hi, v));

        static bool PointInPoly(double px, double py, (double x, double y)[] poly)
        {
            bool inside = false; int n = poly.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = poly[i].x, yi = poly[i].y;
                double xj = poly[j].x, yj = poly[j].y;
                if ((yi > py) != (yj > py) && px < (xj - xi) * (py - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        public static (double x, double y)[] GenPoly(double cx, double cy, double r, int samples, double jitter, SeededRng rng)
        {
            double drift = 0; var pts = new (double x, double y)[samples];
            for (int i = 0; i < samples; i++)
            {
                double theta = 2.0 * Math.PI * i / samples;
                drift += rng.Next() * jitter * 2 - jitter; drift *= 0.9;
                double lr = r * (1.0 + drift); lr = Clamp(lr, r * 0.65, r * 1.4);
                pts[i] = (cx + Math.Cos(theta) * lr, cy + Math.Sin(theta) * lr);
            }
            return pts;
        }

        public static void FillPoly(Terrain[,] grid, int W, int H, (double x, double y)[] poly, Terrain val, Terrain? onlyOver = null)
        {
            double minX = poly.Min(p => p.x), maxX = poly.Max(p => p.x);
            double minY = poly.Min(p => p.y), maxY = poly.Max(p => p.y);
            int x0 = Math.Max(0, (int)Math.Floor(minX));
            int x1 = Math.Min(W - 1, (int)Math.Ceiling(maxX));
            int y0 = Math.Max(0, (int)Math.Floor(minY));
            int y1 = Math.Min(H - 1, (int)Math.Ceiling(maxY));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (PointInPoly(x + 0.5, y + 0.5, poly))
                        if (onlyOver == null || grid[y, x] == onlyOver.Value)
                            grid[y, x] = val;
        }

        public static (int cx, int cy, int r)[] PlaceIslands(int W, int H, int n, int ri0, int ri1, SeededRng rng)
        {
            int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((double)n * W / H)));
            int rows = (int)Math.Ceiling((double)n / cols);
            var cells = new List<(int r, int c)>();
            for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) cells.Add((r, c));
            rng.Shuffle(cells);
            var placed = new List<(int cx, int cy, int r)>();
            for (int i = 0; i < n; i++)
            {
                int radius = rng.Int(ri0, ri1 + 1); bool found = false;
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    int cx, cy;
                    if (attempt < 20)
                    {
                        var (gr, gc) = cells[i % cells.Count]; int cw = W / cols, ch = H / rows;
                        cx = Clamp(gc * cw + rng.Int(0, cw), radius + 2, W - radius - 3);
                        cy = Clamp(gr * ch + rng.Int(0, ch), radius + 2, H - radius - 3);
                    }
                    else { cx = rng.Int(radius + 2, W - radius - 3); cy = rng.Int(radius + 2, H - radius - 3); }
                    bool ok = true; foreach (var (px, py, pr) in placed) { int minDist = pr + radius + 3; if ((cx - px) * (cx - px) + (cy - py) * (cy - py) < minDist * minDist) { ok = false; break; } }
                    if (ok) { placed.Add((cx, cy, radius)); found = true; break; }
                }
                if (!found) { var (gr, gc) = cells[i % cells.Count]; int cw = W / cols, ch = H / rows; int cx = Clamp(gc * cw + rng.Int(0, cw), radius + 2, W - radius - 3); int cy = Clamp(gr * ch + rng.Int(0, ch), radius + 2, H - radius - 3); placed.Add((cx, cy, radius)); }
            }
            return placed.ToArray();
        }

        public static List<(int x, int y)> PickLandPoints(List<(int x, int y)> landCells, int m, SeededRng rng)
        {
            if (landCells.Count == 0) return new(); if (m >= landCells.Count) return new(landCells);
            int cellSize = Math.Max(1, (int)Math.Floor(Math.Sqrt((double)landCells.Count / m)));
            var buckets = new Dictionary<(int, int), List<(int, int)>>();
            foreach (var (x, y) in landCells) { var key = (x / cellSize, y / cellSize); if (!buckets.ContainsKey(key)) buckets[key] = new(); buckets[key].Add((x, y)); }
            var keys = buckets.Keys.ToList(); rng.Shuffle(keys);
            var chosen = new List<(int, int)>();
            for (int i = 0; i < Math.Min(m, keys.Count); i++) { var bucket = buckets[keys[i]]; chosen.Add(bucket[rng.Int(0, bucket.Count)]); }
            int safety = 0; while (chosen.Count < m && chosen.Count < landCells.Count && safety++ < 10000) { var e = landCells[rng.Int(0, landCells.Count)]; if (!chosen.Contains(e)) chosen.Add(e); }
            return chosen;
        }

        public static Terrain[,] Generate(int W, int H, int seed, int ni, int nm, int nx, int ny, int ri0, int ri1, int rm0, int rm1, int rx0, int rx1, int ry0, int ry1, out (int cx, int cy, int r)[] islandInfo)
        {
            var rng = new SeededRng(seed); var grid = new Terrain[H, W];
            islandInfo = PlaceIslands(W, H, ni, ri0, ri1, rng);
            foreach (var (cx, cy, r) in islandInfo) { var poly = GenPoly(cx, cy, r, 80, 0.09, rng); FillPoly(grid, W, H, poly, Terrain.Land); }
            var landCells = CollectCells(grid, W, H, Terrain.Land);
            foreach (var (cx, cy) in PickLandPoints(landCells, nm, rng)) { int r = rng.Int(rm0, rm1 + 1); var poly = GenPoly(cx, cy, r, 48, 0.13, rng); FillPoly(grid, W, H, poly, Terrain.Mountain, Terrain.Land); }
            landCells = CollectCells(grid, W, H, Terrain.Land);
            foreach (var (cx, cy) in PickLandPoints(landCells, nx, rng)) { int r = rng.Int(rx0, rx1 + 1); var poly = GenPoly(cx, cy, r, 40, 0.15, rng); FillPoly(grid, W, H, poly, Terrain.Lake, Terrain.Land); }
            landCells = CollectCells(grid, W, H, Terrain.Land);
            foreach (var (cx, cy) in PickLandPoints(landCells, ny, rng)) { int r = rng.Int(ry0, ry1 + 1); var poly = GenPoly(cx, cy, r, 48, 0.10, rng); FillPoly(grid, W, H, poly, Terrain.Forest, Terrain.Land); }
            return grid;
        }

        static List<(int x, int y)> CollectCells(Terrain[,] grid, int W, int H, Terrain t)
        {
            var list = new List<(int, int)>(); for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (grid[y, x] == t) list.Add((x, y)); return list;
        }
    }

    public class IslandMapGeneratorUnity : MonoBehaviour
    {
        [Header("Map Settings")]
        public int width = 60;
        public int height = 40;
        public int seed = 42;
        public int islands = 3;
        public int mountains = 2;
        public int lakes = 1;
        public int forests = 2;
        public int islandRadiusMin = 6;
        public int islandRadiusMax = 14;
        public int mountainRadiusMin = 2;
        public int mountainRadiusMax = 6;
        public int lakeRadiusMin = 1;
        public int lakeRadiusMax = 4;
        public int forestRadiusMin = 2;
        public int forestRadiusMax = 6;

        public Terrain[,] Grid { get; private set; }

        public void Generate()
        {
            Grid = MapGen.Generate(width, height, seed, islands, mountains, lakes, forests, islandRadiusMin, islandRadiusMax, mountainRadiusMin, mountainRadiusMax, lakeRadiusMin, lakeRadiusMax, forestRadiusMin, forestRadiusMax, out _);
        }

        [ContextMenu("Generate And Print")]
        public void GenerateAndPrint()
        {
            Generate();
            var rows = new string[height];
            for (int y = 0; y < height; y++)
            {
                var chars = new char[width];
                for (int x = 0; x < width; x++) chars[x] = Grid[y, x] == Terrain.Sky ? ' ' : '#';
                rows[y] = new string(chars);
            }
            var sb = string.Join("\n", rows);
            Debug.Log($"Island map (seed={seed}) {width}x{height}:\n" + sb);
        }

        void Start() { GenerateAndPrint(); }
    }
}
