using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MixedStorage
{
    internal static class StorageMeshClipper
    {
        // Managed geometry entry point also exercised by the offline tests.
        internal static Vector3[] ClipTriangle(Vector3 a, Vector3 b, Vector3 c, float minX, float maxX)
        {
            var input = new List<Vertex> { new Vertex { Position = a }, new Vertex { Position = b }, new Vertex { Position = c } };
            var output = new List<Vertex>();
            Cut(input, output, minX, true);
            Cut(output, input, maxX, false);
            var result = new Vector3[input.Count];
            for (int i = 0; i < result.Length; i++) result[i] = input[i].Position;
            return result;
        }
        private struct Vertex
        {
            public Vector3 Position, Normal;
            public Vector4 Tangent;
            public Vector2 UV, UV2;
            public Color Color;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex
            {
                Position = Vector3.LerpUnclamped(a.Position, b.Position, t),
                Normal = Vector3.LerpUnclamped(a.Normal, b.Normal, t).normalized,
                Tangent = Vector4.LerpUnclamped(a.Tangent, b.Tangent, t),
                UV = Vector2.LerpUnclamped(a.UV, b.UV, t), UV2 = Vector2.LerpUnclamped(a.UV2, b.UV2, t),
                Color = Color.LerpUnclamped(a.Color, b.Color, t)
            };
        }

        internal static Mesh Clip(Mesh source, Matrix4x4 transform, float minX, float maxX)
        {
            var points = source.vertices;
            var normals = source.normals;
            var uv = source.uv;
            var uv2 = source.uv2;
            var colors = source.colors;
            var tangents = source.tangents;
            var normalMatrix = transform.inverse.transpose;
            var vertices = new Vertex[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var tangent = tangents.Length == points.Length ? tangents[i] : new Vector4(1, 0, 0, 1);
                var direction = transform.MultiplyVector(new Vector3(tangent.x, tangent.y, tangent.z)).normalized;
                vertices[i] = new Vertex
                {
                    Position = transform.MultiplyPoint3x4(points[i]),
                    Normal = normals.Length == points.Length ? normalMatrix.MultiplyVector(normals[i]).normalized : Vector3.up,
                    UV = uv.Length == points.Length ? uv[i] : Vector2.zero,
                    UV2 = uv2.Length == points.Length ? uv2[i] : Vector2.zero,
                    Color = colors.Length == points.Length ? colors[i] : Color.white,
                    Tangent = new Vector4(direction.x, direction.y, direction.z, tangent.w)
                };
            }
            var output = new List<Vertex>();
            var indices = new List<int>();
            var polygon = new List<Vertex>(5);
            var scratch = new List<Vertex>(5);
            var triangles = source.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                polygon.Clear();
                polygon.Add(vertices[triangles[i]]); polygon.Add(vertices[triangles[i + 1]]); polygon.Add(vertices[triangles[i + 2]]);
                Cut(polygon, scratch, minX, true);
                Cut(scratch, polygon, maxX, false);
                if (polygon.Count < 3) continue;
                int start = output.Count;
                output.AddRange(polygon);
                for (int j = 1; j < polygon.Count - 1; j++)
                { indices.Add(start); indices.Add(start + j); indices.Add(start + j + 1); }
            }
            var positions = new List<Vector3>(output.Count);
            var outNormals = new List<Vector3>(output.Count);
            var outUV = new List<Vector2>(output.Count);
            var outUV2 = new List<Vector2>(output.Count);
            var outColors = new List<Color>(output.Count);
            var outTangents = new List<Vector4>(output.Count);
            foreach (var v in output)
            { positions.Add(v.Position); outNormals.Add(v.Normal); outUV.Add(v.UV); outUV2.Add(v.UV2); outColors.Add(v.Color); outTangents.Add(v.Tangent); }
            var mesh = new Mesh { name = "MixedStorageSection", indexFormat = output.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions); mesh.SetNormals(outNormals); mesh.SetUVs(0, outUV); mesh.SetUVs(1, outUV2);
            mesh.SetColors(outColors); mesh.SetTangents(outTangents); mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Cut(List<Vertex> input, List<Vertex> output, float boundary, bool keepRight)
        {
            output.Clear();
            if (input.Count == 0) return;
            var previous = input[input.Count - 1];
            bool previousInside = keepRight ? previous.Position.x >= boundary : previous.Position.x <= boundary;
            foreach (var current in input)
            {
                bool inside = keepRight ? current.Position.x >= boundary : current.Position.x <= boundary;
                if (inside != previousInside)
                    output.Add(Vertex.Lerp(previous, current, (boundary - previous.Position.x) / (current.Position.x - previous.Position.x)));
                if (inside) output.Add(current);
                previous = current; previousInside = inside;
            }
        }
    }
}
