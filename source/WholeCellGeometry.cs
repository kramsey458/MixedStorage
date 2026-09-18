using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixedStorage
{
    internal static class WholeCellGeometry
    {
        // Native variants concatenate complete primary/secondary meshes. Validate their
        // index topology before treating any vertex range as a cell (including loose faces).
        internal static int[] Select(Vector3[] vertices, int[] indices, int[][] patterns,
            int[] sizes, float min, float max, bool last)
        {
            var output = new List<int>();
            int vertex = 0, triangle = 0;
            while (triangle < indices.Length)
            {
                int match = -1;
                for (int p = 0; p < patterns.Length; p++)
                {
                    var pattern = patterns[p];
                    if (pattern.Length == 0 || triangle + pattern.Length > indices.Length || vertex + sizes[p] > vertices.Length) continue;
                    bool same = true;
                    for (int i = 0; i < pattern.Length; i++)
                        if (indices[triangle + i] != vertex + pattern[i]) { same = false; break; }
                    if (same) { match = p; break; }
                }
                if (match < 0) return null;
                float left = float.PositiveInfinity, right = float.NegativeInfinity;
                for (int i = vertex; i < vertex + sizes[match]; i++)
                { left = Mathf.Min(left, vertices[i].x); right = Mathf.Max(right, vertices[i].x); }
                float center = (left + right) * .5f;
                if (center >= min && (center < max || last && center <= max))
                    for (int i = 0; i < patterns[match].Length; i++) output.Add(indices[triangle + i]);
                triangle += patterns[match].Length;
                vertex += sizes[match];
            }
            return vertex == vertices.Length ? output.ToArray() : null;
        }

        internal static Mesh Build(Mesh source, Matrix4x4 transform, Mesh[] cells,
            float min, float max, bool last)
        {
            var positions = source.vertices;
            for (int i = 0; i < positions.Length; i++) positions[i] = transform.MultiplyPoint3x4(positions[i]);
            var patterns = new List<int[]>();
            var sizes = new List<int>();
            foreach (var cell in cells)
                if (cell != null && cell.isReadable) { patterns.Add(cell.triangles); sizes.Add(cell.vertexCount); }
            var selected = Select(positions, source.triangles, patterns.ToArray(), sizes.ToArray(), min, max, last);
            if (selected == null)
            {
                // Continuous bulk surfaces or unfamiliar topology: fit the complete native
                // mesh to its section instead of cutting faces or guessing cell boundaries.
                float left = float.PositiveInfinity, right = float.NegativeInfinity;
                foreach (var point in positions) { left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x); }
                float scale = right > left ? Mathf.Min(1, (max - min) / (right - left)) : 1;
                var fit = Matrix4x4.Translate(new Vector3((min + max) * .5f, 0, 0)) *
                    Matrix4x4.Scale(new Vector3(scale, 1, 1)) * Matrix4x4.Translate(new Vector3(-(left + right) * .5f, 0, 0));
                transform = fit * transform;
                for (int i = 0; i < positions.Length; i++) positions[i] = fit.MultiplyPoint3x4(positions[i]);
                selected = source.triangles;
            }
            var normals = source.normals;
            var normalMatrix = transform.inverse.transpose;
            for (int i = 0; i < normals.Length; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            var tangents = source.tangents;
            for (int i = 0; i < tangents.Length; i++)
            {
                var t = tangents[i];
                var direction = transform.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
                tangents[i] = new Vector4(direction.x, direction.y, direction.z, t.w);
            }
            var mesh = new Mesh { name = "MixedStorageWholeCells", indexFormat = source.indexFormat };
            mesh.vertices = positions; mesh.normals = normals; mesh.tangents = tangents;
            mesh.uv = source.uv; mesh.uv2 = source.uv2; mesh.colors = source.colors;
            mesh.triangles = selected;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
