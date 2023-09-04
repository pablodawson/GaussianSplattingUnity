#include "UnityCG.cginc"

// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles

struct Cov3D {
    float3 value[6];
};


Cov3D ComputeCov3D(float3 log_scale, float4 rot)
{
    float modifier = 1.0; // Scale modifier
    
    // Scaling Matrix, log scale?
    float3x3 S = float3x3(
        log_scale.x * modifier, 0.0, 0.0,
        0.0, log_scale.y * modifier, 0.0,
        0.0, 0.0, log_scale.z * modifier
    );

    float r = rot.x;
    float x = rot.y;
    float y = rot.z;
    float z = rot.w;
    
    // Compute rotation matrix from quaternion
    float3x3 R = float3x3(
        1.0 - 2.0 * (y * y + z * z), 2.0 * (x * y - r * z), 2.0 * (x * z + r * y),
        2.0 * (x * y + r * z), 1.0 - 2.0 * (x * x + z * z), 2.0 * (y * z - r * x),
        2.0 * (x * z - r * y), 2.0 * (y * z + r * x), 1.0 - 2.0 * (x * x + y * y)
    );

    // 3d world covariance matrix
    float3x3 M = S * R;
    float3x3 Sigma = mul(transpose(M), M);

    Cov3D retu;
    
    retu.value[0] = Sigma[0][0];
    retu.value[1] = Sigma[0][1];
    retu.value[2] = Sigma[0][2];
    retu.value[3] = Sigma[1][1];
    retu.value[4] = Sigma[1][2];
    retu.value[5] = Sigma[2][2];

    return retu;
}

float sigmoid(float x) {
    if (x >= 0.0) {
        return 1.0 / (1.0 + exp(-x));
    } else {
        float z = exp(x);
        return z / (1.0 + z);
    }
};

float3 ComputeCov2D(float3 position, float3 log_scale, float4 rot, float focalX, float focalY, float tanFovX, float tanFovY)
{
    Cov3D _cov3d = ComputeCov3D(log_scale, rot);
    
    float cov3d[6];

    cov3d[0] = _cov3d.value[0];
    cov3d[1] = _cov3d.value[1];
    cov3d[2] = _cov3d.value[2];
    cov3d[3] = _cov3d.value[3];
    cov3d[4] = _cov3d.value[4];
    cov3d[5] = _cov3d.value[5];

    float4 t = mul(UNITY_MATRIX_V, float4(position, 1.0));
    
    float limx = 1.3 * tanFovX;
    float limy = 1.3 * tanFovY;
    float txtz = t.x / t.z;
    float tytz = t.y / t.z;

    t.x = min(limx, max(-limx, txtz)) * t.z;
    t.y = min(limy, max(-limy, tytz)) * t.z;

    float4x4 J = float4x4(
        focalX / t.z, 0.0, -(focalX * t.x) / (t.z * t.z), 0.0,
        0.0, focalY / t.z, -(focalY * t.y) / (t.z * t.z), 0.0,
        0.0, 0.0, 0.0, 0.0,
        0.0, 0.0, 0.0, 0.0
    );

    float4x4 W = transpose((float4x4)UNITY_MATRIX_V);

    float4x4 T = mul(W, J);

    float4x4 Vrk = float4x4(
        cov3d[0], cov3d[1], cov3d[2], 0.0,
        cov3d[1], cov3d[3], cov3d[4], 0.0,
        cov3d[2], cov3d[4], cov3d[5], 0.0,
        0.0, 0.0, 0.0, 0.0
    );

    float4x4 cov = mul(transpose(T), mul(transpose(Vrk), T));

    cov[0][0] += 0.3;
    cov[1][1] += 0.3;

    return float3(cov[0][0], cov[0][1], cov[1][1]);
}

float ndc2pix(float v, uint size)
{
    return ((v + 1.0) * float(size) - 1.0) * 0.5;
};


// spherical harmonic coefficients
float SH_C0 = 0.28209479177387814;
float SH_C1 = 0.4886025119029199;
float SH_C2[5] = {
    1.0925484305920792,
    -1.0925484305920792,
    0.31539156525252005,
    -1.0925484305920792,
    0.5462742152960396
};

float SH_C3[7] = {
    -0.5900435899266435,
    2.890611442640554,
    -0.4570457994644658,
    0.3731763325901154,
    -0.4570457994644658,
    1.445305721320277,
    -0.5900435899266435
};


float3 ComputeColorFromSH(float3 position, float3 sh[16])
{
    float3 dir = normalize(position - (float3)_WorldSpaceCameraPos);

    float3 result = SH_C0 * sh[0];
    
    // if deg > 0
    float x = dir.x;
    float y = dir.y;
    float z = dir.z;

    result += SH_C1 * (-y * sh[1] + z * sh[2] - x * sh[3]);

    float xx = x * x;
    float yy = y * y;
    float zz = z * z;
    float xy = x * y;
    float xz = x * z;
    float yz = y * z;

    // if (sh_degree > 1) {
    result +=
        SH_C2[0] * xy * sh[4] +
        SH_C2[1] * yz * sh[5] +
        SH_C2[2] * (2.0 * zz - xx - yy) * sh[6] +
        SH_C2[3] * xz * sh[7] +
        SH_C2[4] * (xx - yy) * sh[8];

    // if (sh_degree > 2) {
    result +=
        SH_C3[0] * y * (3.0 * xx - yy) * sh[9] +
        SH_C3[1] * xy * z * sh[10] +
        SH_C3[2] * y * (4.0 * zz - xx - yy) * sh[11] +
        SH_C3[3] * z * (2.0 * zz - 3.0 * xx - 3.0 * yy) * sh[12] +
        SH_C3[4] * x * (4.0 * zz - xx - yy) * sh[13] +
        SH_C3[5] * z * (xx - yy) * sh[14] +
        SH_C3[6] * x * (xx - 3.0 * yy) * sh[15];

    // unconditional
    result += 0.5;

    return max(result, float3(0, 0, 0));
}