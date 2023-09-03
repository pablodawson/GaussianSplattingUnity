using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class Renderer : MonoBehaviour
{
    Gaussians gaussians;
    [SerializeField] Shader _gaussianShader = null;
    [SerializeField] Material _meshMaterial;

    GraphicsBuffer indexBuffer;
    ComputeBuffer posBuffer, scaleBuffer, rotQuatBuffer, opacityBuffer, shCoeffsBuffer, gaussianBuffer;
    float focalX, focalY, tanFovX, tanFovY;

    // Start is called before the first frame update
    void Start()
    {
        string plyPath = "C:/Users/Pablo/Downloads/point_cloud.ply";
        gaussians = new Gaussians(File.ReadAllBytes(plyPath));

        // Compute Buffers to shader
        posBuffer = new ComputeBuffer(gaussians.NumGaussians, 3 * sizeof(float));
        posBuffer.SetData(gaussians.Position);
        scaleBuffer = new ComputeBuffer(gaussians.NumGaussians, 3 * sizeof(float));
        scaleBuffer.SetData(gaussians.Scale);
        rotQuatBuffer = new ComputeBuffer(gaussians.NumGaussians, 4 * sizeof(float));
        rotQuatBuffer.SetData(gaussians.RotQuat);
        opacityBuffer = new ComputeBuffer(gaussians.NumGaussians, sizeof(float));
        opacityBuffer.SetData(gaussians.Opacity);
        shCoeffsBuffer = new ComputeBuffer(gaussians.NumGaussians * gaussians.shCoeffs_size, 3 * sizeof(float));
        shCoeffsBuffer.SetData(gaussians.ShCoeffs);
        
        // Indexing, for now sequential. Later needs to be ordered by depth.
        int N = gaussians.NumGaussians * 6 ;
        int[] indexData = new int[N];

        for (int i = 0; i < N; i++)
        {
            indexData[i] = i;
        }

        indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, indexData.Length, 4);
        indexBuffer.SetData(indexData);

        // Camera params
        Camera mainCamera = Camera.main;
        // Focal
        float focalLengthPixels = Screen.height / (2.0f * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
        focalX = focalLengthPixels * Screen.width / Screen.height;
        focalY = focalLengthPixels;

        tanFovX = 0.5f * Screen.width / focalX;
        tanFovY = 0.5f * Screen.height / focalY;

    }
    
    void OnDestroy()
    {
        indexBuffer?.Dispose();
    }

    void Update()
    {
        if (gaussians == null) return;

        // Lazy initialization
        if (_meshMaterial == null)
        {
            _meshMaterial = new Material(_gaussianShader);
        }

        RenderParams rp = new RenderParams(_meshMaterial);
        rp.worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one); // use tighter bounds

        rp.matProps = new MaterialPropertyBlock();

        // Pass data to the shader
        rp.matProps.SetInt("SphericalHarmonicsDegree", gaussians.SphericalHarmonicsDegree);
        rp.matProps.SetInt("n_SphericalCoeff", gaussians.n_SphericalCoeff);
        rp.matProps.SetMatrix("_ObjectToWorld", Matrix4x4.Translate(new Vector3(0, 0, 0)));

        //Camera params
        rp.matProps.SetFloat("focalX", focalX);
        rp.matProps.SetFloat("focalY", focalY);
        rp.matProps.SetFloat("tanFovX", tanFovX);
        rp.matProps.SetFloat("tanFovY", tanFovY);
        
        rp.matProps.SetBuffer("Position", posBuffer);
        rp.matProps.SetBuffer("Scale", scaleBuffer);
        rp.matProps.SetBuffer("RotQuat", rotQuatBuffer);
        rp.matProps.SetBuffer("Opacity", opacityBuffer);
        rp.matProps.SetBuffer("ShCoeffs", shCoeffsBuffer);

        // Draw
        Graphics.RenderPrimitivesIndexed(rp, MeshTopology.Triangles, indexBuffer, indexBuffer.count, 0 , gaussians.NumGaussians);
    }
}
