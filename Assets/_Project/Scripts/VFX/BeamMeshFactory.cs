using UnityEngine;

/// <summary>
/// Runtime-generated shared meshes so the VFX prefab needs zero mesh assets.
/// Unit cylinder: axis +Z, z in [0,1], radius 0.5, open-ended, outward winding (Cull Back safe).
/// Quad: 1x1 centered, uv 0..1 (shaders remap to -1..1).
/// </summary>
public static class BeamMeshFactory
{
    private static Mesh unitCylinder;
    private static Mesh unitCylinderLow;
    private static Mesh quad;

    public static Mesh GetUnitCylinder(int sides)
    {
        bool low = sides < 10;
        if (low)
        {
            if (unitCylinderLow != null) return unitCylinderLow;
        }
        else if (unitCylinder != null)
        {
            return unitCylinder;
        }

        sides = Mathf.Clamp(sides, 6, 32);
        int ringVerts = sides + 1;                 // seam duplicated for clean UVs
        var vertices = new Vector3[ringVerts * 2];
        var normals = new Vector3[ringVerts * 2];
        var uvs = new Vector2[ringVerts * 2];

        for (int ring = 0; ring < 2; ring++)
        {
            for (int i = 0; i <= sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                int idx = ring * ringVerts + i;
                vertices[idx] = new Vector3(cs * 0.5f, sn * 0.5f, ring);   // ring 0 at z=0, ring 1 at z=1
                normals[idx] = new Vector3(cs, sn, 0f);
                uvs[idx] = new Vector2(i / (float)sides, ring);
            }
        }

        var triangles = new int[sides * 6];
        int t = 0;
        for (int i = 0; i < sides; i++)
        {
            int b0 = i, b1 = i + 1;                         // bottom ring (z=0)
            int t0 = ringVerts + i, t1 = ringVerts + i + 1; // top ring (z=1)
            triangles[t++] = b0; triangles[t++] = b1; triangles[t++] = t0;
            triangles[t++] = b1; triangles[t++] = t1; triangles[t++] = t0;
        }

        Mesh mesh = new Mesh { name = "SpellBeamUnitCylinder" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);

        if (low) unitCylinderLow = mesh; else unitCylinder = mesh;
        return mesh;
    }

    public static Mesh GetQuad()
    {
        if (quad != null) return quad;
        quad = new Mesh { name = "SpellBeamQuad" };
        quad.SetVertices(new[] {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f), new Vector3(0.5f,  0.5f, 0f) });
        quad.SetUVs(0, new[] {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f) });
        quad.SetTriangles(new[] { 0, 3, 1, 0, 2, 3 }, 0);
        quad.RecalculateBounds();
        quad.UploadMeshData(false);
        return quad;
    }
}
