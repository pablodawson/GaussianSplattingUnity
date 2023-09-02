using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class Renderer : MonoBehaviour
{
    GaussiansStruct gaussians;
    [SerializeField] Shader _gaussianShader = null;
    Material _meshMaterial;
    [SerializeField] Color _pointTint = new Color(0.5f, 0.5f, 0.5f, 1);

    // Start is called before the first frame update
    void Start()
    {
        string plyPath = "C:/Users/pablo_7xoop1s/Downloads/point_cloud.ply";
        gaussians = new GaussiansStruct(File.ReadAllBytes(plyPath));
        Debug.Log("Loaded gaussians");
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
            _meshMaterial.hideFlags = HideFlags.DontSave;
            _meshMaterial.EnableKeyword("_COMPUTE_BUFFER");
        }
        
        _meshMaterial.SetPass(0);
        _meshMaterial.SetMatrix("_Transform", transform.localToWorldMatrix);
        _meshMaterial.SetInt("SphericalHarmonicsDegree", gaussians.SphericalHarmonicsDegree);
        _meshMaterial.SetInt("n_SphericalCoeff", gaussians.n_SphericalCoeff);
        _meshMaterial.SetFloat("_PointSize", 1f);
        _meshMaterial.SetColor("_Tint", _pointTint);

        // Compute Buffers
        ComputeBuffer gaussianBuffer = new ComputeBuffer(gaussians.NumGaussians, sizeof(float) * (3 + 3 + 4 + 1 + gaussians.shCoeffs_size * 3), ComputeBufferType.Default);
        gaussianBuffer.SetData(gaussians.gaussianArray);
        _meshMaterial.SetBuffer("gaussians", gaussianBuffer);
        gaussianBuffer.Dispose();


#if UNITY_2019_1_OR_NEWER
        Graphics.DrawProceduralNow(MeshTopology.Points, gaussians.NumGaussians, 1);
#else
        Graphics.DrawProcedural(MeshTopology.Points, gaussians.NumGaussians, 1);
#endif
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
