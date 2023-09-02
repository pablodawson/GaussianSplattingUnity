using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class RendererQuads : MonoBehaviour
{
    GaussiansStruct gaussians;
    [SerializeField] Shader _gaussianShader = null;
    [SerializeField] Material _meshMaterial;
    Mesh primitive;

    GraphicsBuffer indexBuffer;
    

    // Start is called before the first frame update
    void Start()
    {
        // Load Gaussians
        string plyPath = "C:/Users/pablo_7xoop1s/Downloads/point_cloud.ply";
        gaussians = new GaussiansStruct(File.ReadAllBytes(plyPath));
        Debug.Log("Loaded gaussians");

        // For now sequential. Then needs to be ordered by depth.
        int N = gaussians.NumGaussians * 6 ;
        int[] indexData = new int[N];

        for (int i = 0; i < N; i++)
        {
            indexData[i] = i;
        }

        indexBuffer.SetData(indexData);
    }
    
    void OnDestroy()
    {
        if (_meshMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(_meshMaterial);
            }
            else
            {
                DestroyImmediate(_meshMaterial);
            }
        }
    }

    void OnRenderObject()
    {
        if (gaussians == null) return;

        // Check the camera condition.
        var camera = Camera.current;
        if ((camera.cullingMask & (1 << gameObject.layer)) == 0) return;

        // Lazy initialization
        if (_meshMaterial == null)
        {
            _meshMaterial = new Material(_gaussianShader);
        }

        // Compute Buffers
        ComputeBuffer gaussianBuffer = new ComputeBuffer(gaussians.NumGaussians, sizeof(float) * (3 + 3 + 4 + 1 + gaussians.shCoeffs_size * 3), ComputeBufferType.Default);
        gaussianBuffer.SetData(gaussians.gaussianArray);
                
        gaussianBuffer.Dispose();

        RenderParams rp = new RenderParams(_meshMaterial);
        rp.worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one); // use tighter bounds

        rp.matProps = new MaterialPropertyBlock();

        // Pass data to the shader
        rp.matProps.SetInt("SphericalHarmonicsDegree", gaussians.SphericalHarmonicsDegree);
        rp.matProps.SetBuffer("gaussians", gaussianBuffer);
        rp.matProps.SetInt("n_SphericalCoeff", gaussians.n_SphericalCoeff);

        rp.matProps.SetMatrix("_ObjectToWorld", Matrix4x4.Translate(new Vector3(-4.5f, 0, 0)));
        rp.matProps.SetFloat("_NumInstances", 10.0f);

        Graphics.RenderPrimitivesIndexed(rp, MeshTopology.Triangles, indexBuffer, indexBuffer.count, 0 , gaussians.NumGaussians);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
