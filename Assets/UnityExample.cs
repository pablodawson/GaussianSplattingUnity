using UnityEngine;

public class UnityExample : MonoBehaviour
{
    public Material material;
    public Mesh mesh;

    GraphicsBuffer meshTriangles;
    GraphicsBuffer meshPositions;

    void Start()
    {
        // note: remember to check "Read/Write" on the mesh asset to get access to the geometry data
        meshTriangles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, mesh.triangles.Length, sizeof(int));
        meshTriangles.SetData(mesh.triangles);
        meshPositions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, mesh.vertices.Length, 3 * sizeof(float));
        meshPositions.SetData(mesh.vertices);
    }

    void OnDestroy()
    {
        meshTriangles?.Dispose();
        meshTriangles = null;
        meshPositions?.Dispose();
        meshPositions = null;
    }

    void Update()
    {
        RenderParams rp = new RenderParams(material);
        rp.worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one); // use tighter bounds
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer("_Positions", meshPositions);
        rp.matProps.SetInt("_BaseVertexIndex", (int)mesh.GetBaseVertex(0));
        rp.matProps.SetMatrix("_ObjectToWorld", Matrix4x4.Translate(new Vector3(-4.5f, 0, 0)));
        rp.matProps.SetFloat("_NumInstances", 10.0f);
        Graphics.RenderPrimitivesIndexed(rp, MeshTopology.Triangles, meshTriangles, meshTriangles.count, (int)mesh.GetIndexStart(0), 10);
    }
}
