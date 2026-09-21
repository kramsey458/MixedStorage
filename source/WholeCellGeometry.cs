using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MixedStorage
{
    // Vertex streams of one mesh in reusable lists, so rebuilding a section does not allocate per-vertex arrays.
    internal sealed class MeshData
    {
        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector4> Tangents = new List<Vector4>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<Vector2> UV2 = new List<Vector2>();
        public readonly List<Color32> Colors = new List<Color32>();
        public readonly List<int> Indices = new List<int>();

        public void Clear()
        {
            Positions.Clear(); Normals.Clear(); Tangents.Clear(); UV.Clear(); UV2.Clear(); Colors.Clear(); Indices.Clear();
        }
    }

    internal static class WholeCellGeometry
    {
        private sealed class CellShape
        {
            public int[] Triangles;
            public int Vertices;
        }

        // The cell meshes are shared native assets, so their topology is read once.
        private static readonly ConditionalWeakTable<Mesh, CellShape> Shapes = new ConditionalWeakTable<Mesh, CellShape>();
        private static readonly ConditionalWeakTable<Mesh, CellShape>.CreateValueCallback ReadShape =
            cell => new CellShape { Triangles = cell.triangles, Vertices = cell.vertexCount };
        // Sections are built one at a time on the main thread; the scratch buffers keep their capacity between builds.
        private static readonly MeshData Source = new MeshData();
        private static readonly MeshData Section = new MeshData();
        private static readonly List<int> SubmeshIndices = new List<int>();
        private static readonly List<int> CellStarts = new List<int>();
        private static readonly List<int> CellKinds = new List<int>();

        // Native variants concatenate complete primary/secondary meshes. Validate their index topology
        // before treating any vertex range as a cell (including loose faces), then copy only the cells
        // whose center lies in [min, max) once transformed. Returns false for unrecognized topology.
        internal static bool Extract(MeshData source, Matrix4x4 transform, Matrix4x4 normalMatrix,
            int[][] patterns, int[] sizes, float min, float max, bool last, MeshData output)
        {
            output.Clear();
            CellStarts.Clear();
            CellKinds.Clear();
            var positions = source.Positions;
            var indices = source.Indices;
            int vertex = 0, triangle = 0;
            while (triangle < indices.Count)
            {
                int match = Match(indices, triangle, vertex, positions.Count, patterns, sizes);
                if (match < 0) return false;
                // Only the x coordinate decides ownership, so the full transform is left for the copied cells.
                float left = float.PositiveInfinity, right = float.NegativeInfinity;
                for (int i = vertex; i < vertex + sizes[match]; i++)
                {
                    var point = positions[i];
                    float x = (transform.m00 * point.x + transform.m01 * point.y + transform.m02 * point.z) + transform.m03;
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
                float center = (left + right) * .5f;
                if (center >= min && (center < max || last && center <= max))
                {
                    CellStarts.Add(vertex);
                    CellKinds.Add(match);
                }
                triangle += patterns[match].Length;
                vertex += sizes[match];
            }
            if (vertex != positions.Count) return false;
            bool translation = IsTranslation(transform);
            for (int cell = 0; cell < CellStarts.Count; cell++)
            {
                int kind = CellKinds[cell], baseIndex = output.Positions.Count;
                CopyVertices(source, CellStarts[cell], sizes[kind], transform, normalMatrix, translation, output);
                var pattern = patterns[kind];
                for (int i = 0; i < pattern.Length; i++) output.Indices.Add(baseIndex + pattern[i]);
            }
            return true;
        }

        private static int Match(List<int> indices, int triangle, int vertex, int vertexCount, int[][] patterns, int[] sizes)
        {
            for (int p = 0; p < patterns.Length; p++)
            {
                var pattern = patterns[p];
                if (pattern.Length == 0 || triangle + pattern.Length > indices.Count || vertex + sizes[p] > vertexCount) continue;
                bool same = true;
                for (int i = 0; i < pattern.Length; i++)
                    if (indices[triangle + i] != vertex + pattern[i]) { same = false; break; }
                if (same) return p;
            }
            return -1;
        }

        // Continuous bulk surfaces or unfamiliar topology: fit the complete native mesh to its section
        // instead of cutting faces or guessing cell boundaries. Returns the source-to-section transform.
        internal static Matrix4x4 FitWhole(MeshData source, Matrix4x4 transform, float min, float max)
        {
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            foreach (var point in source.Positions)
            {
                float x = (transform.m00 * point.x + transform.m01 * point.y + transform.m02 * point.z) + transform.m03;
                if (x < left) left = x;
                if (x > right) right = x;
            }
            float scale = right > left ? Mathf.Min(1, (max - min) / (right - left)) : 1;
            var fit = Matrix4x4.Translate(new Vector3((min + max) * .5f, 0, 0)) *
                Matrix4x4.Scale(new Vector3(scale, 1, 1)) * Matrix4x4.Translate(new Vector3(-(left + right) * .5f, 0, 0));
            return fit * transform;
        }

        internal static void TransformAll(MeshData source, Matrix4x4 transform, Matrix4x4 normalMatrix, MeshData output)
        {
            output.Clear();
            CopyVertices(source, 0, source.Positions.Count, transform, normalMatrix, IsTranslation(transform), output);
            output.Indices.AddRange(source.Indices);
        }

        private static void CopyVertices(MeshData source, int start, int count, Matrix4x4 transform,
            Matrix4x4 normalMatrix, bool translation, MeshData output)
        {
            int total = source.Positions.Count;
            bool normals = source.Normals.Count == total, tangents = source.Tangents.Count == total;
            bool uv = source.UV.Count == total, uv2 = source.UV2.Count == total, colors = source.Colors.Count == total;
            var offset = new Vector3(transform.m03, transform.m13, transform.m23);
            for (int i = start; i < start + count; i++)
            {
                output.Positions.Add(translation ? source.Positions[i] + offset : transform.MultiplyPoint3x4(source.Positions[i]));
                if (normals)
                    output.Normals.Add((translation ? source.Normals[i] : normalMatrix.MultiplyVector(source.Normals[i])).normalized);
                if (tangents)
                {
                    var tangent = source.Tangents[i];
                    var direction = new Vector3(tangent.x, tangent.y, tangent.z);
                    direction = (translation ? direction : transform.MultiplyVector(direction)).normalized;
                    output.Tangents.Add(new Vector4(direction.x, direction.y, direction.z, tangent.w));
                }
                if (uv) output.UV.Add(source.UV[i]);
                if (uv2) output.UV2.Add(source.UV2[i]);
                if (colors) output.Colors.Add(source.Colors[i]);
            }
        }

        // Nearly every native variant sits at a plain offset from its parent. Normals and tangents then
        // need no rotation: positions are only moved, and directions are just renormalized.
        internal static bool IsTranslation(Matrix4x4 m) =>
            Near(m.m00, 1) && Near(m.m11, 1) && Near(m.m22, 1) &&
            Near(m.m01, 0) && Near(m.m02, 0) && Near(m.m10, 0) && Near(m.m12, 0) && Near(m.m20, 0) && Near(m.m21, 0);

        private static bool Near(float a, float b) => Math.Abs(a - b) < 1e-6f;

        // Builds the section mesh for [min, max) from a native variant, or null when it has nothing to draw.
        internal static Mesh Build(Mesh source, Matrix4x4 transform, Mesh[] cells, float min, float max, bool last)
        {
            Read(source, Source);
            var patterns = new List<int[]>(cells.Length);
            var sizes = new List<int>(cells.Length);
            foreach (var cell in cells)
            {
                if (cell == null || !cell.isReadable) continue;
                var shape = Shapes.GetValue(cell, ReadShape);
                patterns.Add(shape.Triangles);
                sizes.Add(shape.Vertices);
            }
            var normalMatrix = IsTranslation(transform) ? Matrix4x4.identity : transform.inverse.transpose;
            if (!Extract(Source, transform, normalMatrix, patterns.ToArray(), sizes.ToArray(), min, max, last, Section))
            {
                var whole = FitWhole(Source, transform, min, max);
                TransformAll(Source, whole, IsTranslation(whole) ? Matrix4x4.identity : whole.inverse.transpose, Section);
            }
            return Section.Indices.Count == 0 ? null : Write(Section, source.indexFormat);
        }

        private static void Read(Mesh source, MeshData data)
        {
            data.Clear();
            source.GetVertices(data.Positions);
            source.GetNormals(data.Normals);
            source.GetTangents(data.Tangents);
            source.GetUVs(0, data.UV);
            source.GetUVs(1, data.UV2);
            source.GetColors(data.Colors);
            // Same result as Mesh.triangles: every submesh, in order.
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                if (submesh == 0) source.GetTriangles(data.Indices, 0);
                else
                {
                    source.GetTriangles(SubmeshIndices, submesh);
                    data.Indices.AddRange(SubmeshIndices);
                }
            }
        }

        private static Mesh Write(MeshData data, UnityEngine.Rendering.IndexFormat indexFormat)
        {
            var mesh = new Mesh { name = "MixedStorageWholeCells", indexFormat = indexFormat };
            mesh.SetVertices(data.Positions);
            if (data.Normals.Count > 0) mesh.SetNormals(data.Normals);
            if (data.Tangents.Count > 0) mesh.SetTangents(data.Tangents);
            if (data.UV.Count > 0) mesh.SetUVs(0, data.UV);
            if (data.UV2.Count > 0) mesh.SetUVs(1, data.UV2);
            if (data.Colors.Count > 0) mesh.SetColors(data.Colors);
            mesh.SetTriangles(data.Indices, 0, false);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
