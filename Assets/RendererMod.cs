// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

using UnityEngine;
using System.IO;

namespace Pcx
{
    /// A renderer class that renders a point cloud contained by PointCloudData.
    public sealed class RendererMod : MonoBehaviour
    {
        #region Editable attributes

        [SerializeField] PointCloudData _sourceData = null;

        public PointCloudData sourceData
        {
            get { return _sourceData; }
            set { _sourceData = value; }
        }

        [SerializeField] Color _pointTint = new Color(0.5f, 0.5f, 0.5f, 1);

        public Color pointTint
        {
            get { return _pointTint; }
            set { _pointTint = value; }
        }

        [SerializeField] float _pointSize = 0.05f;

        public float pointSize
        {
            get { return _pointSize; }
            set { _pointSize = value; }
        }

        #endregion

        #region Public properties (nonserialized)

        public ComputeBuffer sourceBuffer { get; set; }

        #endregion

        #region Internal resources

        [SerializeField] Shader _pointShader = null;
        [SerializeField] Shader _diskShader = null;

        #endregion

        #region Private objects

        Material _pointMaterial;
        Material _diskMaterial;
        Gaussians gaussians;
        #endregion

        #region MonoBehaviour implementation

        void OnValidate()
        {
            _pointSize = Mathf.Max(0, _pointSize);
        }

        void OnDestroy()
        {
            if (_pointMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_pointMaterial);
                    Destroy(_diskMaterial);
                }
                else
                {
                    DestroyImmediate(_pointMaterial);
                    DestroyImmediate(_diskMaterial);
                }
            }
        }

        void Start()
        {
            string plyPath = "C:/Users/pablo_7xoop1s/Downloads/point_cloud.ply";
            gaussians = new Gaussians(File.ReadAllBytes(plyPath));
            Debug.Log("Loaded gaussians");
        }

        void OnRenderObject()
        {
            // We need a source data or an externally given buffer.
            if (_sourceData == null && sourceBuffer == null) return;
            if (gaussians == null) return;

            // Check the camera condition.
            var camera = Camera.current;
            if ((camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            if (camera.name == "Preview Scene Camera") return;

            // TODO: Do view frustum culling here.

            // Lazy initialization
            if (_pointMaterial == null)
            {
                _pointMaterial = new Material(_pointShader);
                _pointMaterial.hideFlags = HideFlags.DontSave;
                _pointMaterial.EnableKeyword("_COMPUTE_BUFFER");

                _diskMaterial = new Material(_diskShader);
                _diskMaterial.hideFlags = HideFlags.DontSave;
                _diskMaterial.EnableKeyword("_COMPUTE_BUFFER");
            }

            // Use the external buffer if given any.
            var pointBuffer = sourceBuffer != null ?
                sourceBuffer : _sourceData.computeBuffer;


            _diskMaterial.SetPass(0);
            _diskMaterial.SetColor("_Tint", _pointTint);
            _diskMaterial.SetMatrix("_Transform", transform.localToWorldMatrix);
            _diskMaterial.SetInt("SphericalHarmonicsDegree", gaussians.SphericalHarmonicsDegree);
            _diskMaterial.SetInt("n_SphericalCoeff", gaussians.n_SphericalCoeff);
            //_diskMaterial.SetBuffer("_PointBuffer", pointBuffer);
            _diskMaterial.SetFloat("_PointSize", pointSize);

            // Compute Buffers
            ComputeBuffer positionBuffer = new ComputeBuffer(gaussians.NumGaussians, sizeof(float) * 3, ComputeBufferType.Default);
            ComputeBuffer scaleBuffer = new ComputeBuffer(gaussians.NumGaussians, sizeof(float) * 3, ComputeBufferType.Default);
            ComputeBuffer rotQuatBuffer = new ComputeBuffer(gaussians.NumGaussians, sizeof(float) * 4, ComputeBufferType.Default);
            ComputeBuffer opacityBuffer = new ComputeBuffer(gaussians.NumGaussians, sizeof(float), ComputeBufferType.Default);
            ComputeBuffer shCoeffsBuffer = new ComputeBuffer(gaussians.NumGaussians * gaussians.shCoeffs_size, sizeof(float) * 3, ComputeBufferType.Default);

            positionBuffer.SetData(gaussians.Position);
            scaleBuffer.SetData(gaussians.Scale);
            rotQuatBuffer.SetData(gaussians.RotQuat);
            opacityBuffer.SetData(gaussians.Opacity);
            shCoeffsBuffer.SetData(gaussians.ShCoeffs);
            
            _diskMaterial.SetBuffer("_Position", positionBuffer);
            _diskMaterial.SetBuffer("_Scale", scaleBuffer);
            _diskMaterial.SetBuffer("_RotQuat", rotQuatBuffer);
            _diskMaterial.SetBuffer("_Opacity", opacityBuffer);
            _diskMaterial.SetBuffer("_ShCoeffs", shCoeffsBuffer);

            positionBuffer.Dispose();
            scaleBuffer.Dispose();
            rotQuatBuffer.Dispose();
            opacityBuffer.Dispose();
            shCoeffsBuffer.Dispose();


#if UNITY_2019_1_OR_NEWER
            Graphics.DrawProceduralNow(MeshTopology.Points, pointBuffer.count, 1);
#else
            Graphics.DrawProcedural(MeshTopology.Points, pointBuffer.count, 1);
#endif
        }

        #endregion
    }
}
