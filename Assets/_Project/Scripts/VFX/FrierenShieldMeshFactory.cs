using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-generated shared meshes so the shield prefab needs zero mesh assets.
/// GetCurvedHexDisc: shallow-dished convex circular shield surface facing +Z.
/// Unit circumradius 0.5 (matches GetUnitQuad footprint), normalized UVs (affine in
/// position.xy so the shader's hex tiling stays perfectly regular), analytic outward
/// normals for the paraboloid dome z = curvature * (1 - f^2).
/// GetUnitQuad: 1x1 centered quad for the magic circle / shatter flash planes.
/// </summary>
public static class FrierenShieldMeshFactory
{
    private static readonly Dictionary<int, Mesh> cache = new();
    private static Mesh unitQuad;

    public static Mesh GetCurvedHexDisc(int segments, float curvature)
    {
        segments = Mathf.Clamp(segments, 3, 12);
        curvature = Mathf.Clamp(curvature, -0.3f, 0.3f);

        int key = segments * 1000 + Mathf.RoundToInt(curvature * 500f);
        if (cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        const float radius = 0.5f;                       // unit circumradius
        int slices = segments * 4;                       // angular resolution keeps the rim circular
        int vertexCount = 1 + slices * segments;         // apex + rings

        var vertices = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];

        // Apex: dome pole.
        vertices[0] = new Vector3(0f, 0f, curvature);
        normals[0] = Vector3.forward;
        uvs[0] = new Vector2(0.5f, 0.5f);

        // Ring template on the unit circle (CCW from +X).
        var template = new Vector2[slices];
        for (int j = 0; j < slices; j++)
        {
            float angle = (j / (float)slices) * Mathf.PI * 2f;
            template[j] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        int v = 1;
        for (int ring = 1; ring <= segments; ring++)
        {
            float f = ring / (float)segments;            // 0 center .. 1 rim
            for (int j = 0; j < slices; j++)
            {
                Vector2 xy = template[j] * f;
                float z = curvature * (1f - f * f);

                vertices[v] = new Vector3(xy.x, xy.y, z);

                // Analytic normal for the parametric surface P(f, u) = (f * template, c * (1 - f^2)):
                // dP/df = (template, -2cf); dP/du = perp(template, 0) -> N = (2cf*uy, -2cf*ux, |template|^2).
                Vector2 u = new Vector2(-template[j].y, template[j].x);
                normals[v] = new Vector3(
                    2f * curvature * f * u.y,
                    -2f * curvature * f * u.x,
                    template[j].sqrMagnitude).normalized;

                uvs[v] = xy + new Vector2(0.5f, 0.5f);   // normalized UVs, affine in position.xy
                v++;
            }
        }

        // Center fan (ring 1) + quad strips between rings. CCW winding seen from +Z (Cull Back safe).
        var triangles = new int[3 * slices * (2 * segments - 1)];
        int t = 0;
        for (int j = 0; j < slices; j++)
        {
            triangles[t++] = 0;
            triangles[t++] = 1 + j;
            triangles[t++] = 1 + (j + 1) % slices;
        }
        for (int ring = 1; ring < segments; ring++)
        {
            int innerBase = 1 + (ring - 1) * slices;
            int outerBase = 1 + ring * slices;
            for (int j = 0; j < slices; j++)
            {
                int jn = (j + 1) % slices;
                int a = innerBase + j;    // inner j
                int b = innerBase + jn;   // inner j+1
                int c = outerBase + jn;   // outer j+1
                int d = outerBase + j;    // outer j
                triangles[t++] = a; triangles[t++] = d; triangles[t++] = c;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
            }
        }

        Mesh mesh = new Mesh { name = "FrierenCurvedHexDisc" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);

        cache[key] = mesh;
        return mesh;
    }

    public static Mesh GetUnitQuad()
    {
        if (unitQuad != null) return unitQuad;

        unitQuad = new Mesh { name = "FrierenShieldQuad" };
        unitQuad.SetVertices(new[] {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f), new Vector3(0.5f,  0.5f, 0f) });
        unitQuad.SetUVs(0, new[] {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f) });
        unitQuad.SetTriangles(new[] { 0, 3, 1, 0, 2, 3 }, 0);
        unitQuad.RecalculateBounds();
        unitQuad.UploadMeshData(false);
        return unitQuad;
    }
}
