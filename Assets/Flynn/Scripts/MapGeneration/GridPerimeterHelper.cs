using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility for extracting the outer corner-vertex perimeter from an arbitrary
/// collection of occupied cells in a 2D grid.
///
/// Cells are identified by (x, y) column/row indices. The world-space position
/// of each tile centre and each corner is determined by the supplied
/// <see cref="TileToWorld"/> and <see cref="CornerToWorld"/> delegates, so the
/// helper is agnostic to tile size, origin, and axis mapping.
/// </summary>
public static class GridPerimeterHelper
{
    public delegate Vector3 TileToWorldFn(int x, int y);
    public delegate Vector3 CornerToWorldFn(int cornerX, int cornerY);

    /// <summary>
    /// Returns an ordered list of world-space corner positions that trace the
    /// external boundary of the supplied cell region.
    ///
    /// Each boundary edge lies between a cell that IS in the region and one that
    /// is NOT (or is out of bounds). Because every corner of a simply-connected
    /// region's perimeter has exactly degree 2, a single graph walk produces a
    /// clean closed polygon.
    ///
    /// For a multi-island disconnected region the walk will only capture one
    /// contiguous boundary. Split disconnected regions into separate calls.
    /// </summary>
    /// <param name="cells">All (x,y) cells belonging to the region.</param>
    /// <param name="gridWidth">Total number of columns in the grid.</param>
    /// <param name="gridHeight">Total number of rows in the grid.</param>
    /// <param name="cornerToWorld">
    ///   Maps a corner index (cx, cy) to a world position.
    ///   Corner (cx, cy) sits at the intersection of tiles (cx-1,cy-1),
    ///   (cx,cy-1), (cx-1,cy) and (cx,cy).
    /// </param>
    public static List<Vector3> ComputeCornerPerimeter(
        IEnumerable<(int x, int y)> cells,
        int gridWidth,
        int gridHeight,
        CornerToWorldFn cornerToWorld)
    {
        var cellSet = new HashSet<(int, int)>(cells);

        // Build adjacency: boundary corners -> their two neighbour corners.
        var adjacency = new Dictionary<(int, int), List<(int, int)>>();

        void AddEdge((int, int) a, (int, int) b)
        {
            if (!adjacency.ContainsKey(a)) adjacency[a] = new List<(int, int)>(2);
            if (!adjacency.ContainsKey(b)) adjacency[b] = new List<(int, int)>(2);
            adjacency[a].Add(b);
            adjacency[b].Add(a);
        }

        bool InRegion(int x, int y) =>
            x >= 0 && x < gridWidth && y >= 0 && y < gridHeight && cellSet.Contains((x, y));

        foreach (var (tx, ty) in cellSet)
        {
            // For each of the 4 edges of tile (tx,ty): emit the edge as a pair
            // of corner indices when the neighbour on the far side is NOT in
            // the region (sky / OOB).
            if (!InRegion(tx,     ty - 1)) AddEdge((tx,     ty    ), (tx + 1, ty    )); // south
            if (!InRegion(tx,     ty + 1)) AddEdge((tx,     ty + 1), (tx + 1, ty + 1)); // north
            if (!InRegion(tx - 1, ty    )) AddEdge((tx,     ty    ), (tx,     ty + 1)); // west
            if (!InRegion(tx + 1, ty    )) AddEdge((tx + 1, ty    ), (tx + 1, ty + 1)); // east
        }

        if (adjacency.Count == 0) return new List<Vector3>();

        // Walk ALL loops in the degree-2 graph and keep the one with the
        // largest area — that is always the outer boundary. Regions with
        // interior holes (e.g. grass surrounding water) produce a second,
        // smaller inner-hole loop which we discard here.
        var unvisited = new HashSet<(int, int)>(adjacency.Keys);
        List<(int, int)> bestLoop = null;
        double bestArea = -1d;

        while (unvisited.Count > 0)
        {
            var loopEnum = unvisited.GetEnumerator();
            loopEnum.MoveNext();
            (int, int) startCorner = loopEnum.Current;

            var loop = new List<(int, int)>();
            (int, int) prev = (-1, -1);
            (int, int) curr = startCorner;

            while (true)
            {
                loop.Add(curr);
                unvisited.Remove(curr);

                (int, int) next = (-1, -1);
                foreach (var nb in adjacency[curr])
                {
                    if (nb != prev) { next = nb; break; }
                }

                if (next == startCorner || next == (-1, -1)) break;
                prev = curr;
                curr = next;
            }

            // Shoelace formula on corner indices to get area.
            double area = 0d;
            for (int i = 0; i < loop.Count; i++)
            {
                var (ax, ay) = loop[i];
                var (bx, by) = loop[(i + 1) % loop.Count];
                area += (double)ax * by - (double)bx * ay;
            }
            area = area < 0d ? -area : area;

            if (area > bestArea)
            {
                bestArea = area;
                bestLoop = loop;
            }
        }

        if (bestLoop == null) return new List<Vector3>();

        // Map corner indices -> world positions.
        var result = new List<Vector3>(bestLoop.Count);
        foreach (var (cx, cy) in bestLoop)
            result.Add(cornerToWorld(cx, cy));

        return result;
    }

    /// <summary>
    /// Convenience overload that derives the corner-to-world mapping from a
    /// tile-centre-to-world function. Assumes tiles are axis-aligned unit squares
    /// so corners sit half a unit from each tile centre.
    ///
    /// Corner (cx, cy) world position = tileToWorld(cx, cy) - offset,
    /// where offset compensates for the half-tile shift between corner and
    /// centre coordinates.
    /// </summary>
    public static List<Vector3> ComputeCornerPerimeter(
        IEnumerable<(int x, int y)> cells,
        int gridWidth,
        int gridHeight,
        TileToWorldFn tileToWorld)
    {
        // Determine the half-tile offset by sampling how tileToWorld advances
        // per unit step, then halving it. This works for any axis mapping.
        Vector3 origin = tileToWorld(0, 0);
        Vector3 stepX  = tileToWorld(1, 0) - origin;
        Vector3 stepY  = tileToWorld(0, 1) - origin;
        Vector3 half   = (stepX + stepY) * 0.5f;

        CornerToWorldFn cornerToWorld = (cx, cy) => tileToWorld(cx, cy) - half;

        return ComputeCornerPerimeter(cells, gridWidth, gridHeight, cornerToWorld);
    }
}
