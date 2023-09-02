#include "UnityCG.cginc"
// Upgrade NOTE: excluded shader from DX11 because it uses wrong array syntax (type[size] name)
#pragma exclude_renderers d3d11
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles



float4 ComputeCov3D(float3 log_scale, float4 rot)
{
    float modifier = 1.0; // Scale modifier
    
    float3x3 S = float3x3(
        exp(log_scale.x) * modifier, 0.0, 0.0,
        0.0, exp(log_scale.y) * modifier, 0.0,
        0.0, 0.0, exp(log_scale.z) * modifier
    );

    float r = rot.x;
    float x = rot.y;
    float y = rot.z;
    float z = rot.w;

    float3x3 R = float3x3(
        1.0 - 2.0 * (y * y + z * z), 2.0 * (x * y - r * z), 2.0 * (x * z + r * y),
        2.0 * (x * y + r * z), 1.0 - 2.0 * (x * x + z * z), 2.0 * (y * z - r * x),
        2.0 * (x * z - r * y), 2.0 * (y * z + r * x), 1.0 - 2.0 * (x * x + y * y)
    );

    float3x3 M = S * R;
    float3x3 Sigma = mul(transpose(M), M);

    float ret[6]
    
    ret[0] = Sigma[0][0];
    ret[1] = Sigma[0][1];
    ret[2] = SigmaSigma[0][2];
    ret[3] = SigmaSigma[1][1];
    ret[4] = SigmaSigma[1][2];
    ret[5] = SigmaSigma[2][2];

    return ret
}

float sigmoid(float x) {
    if (x >= 0.0) {
        return 1.0 / (1.0 + exp(-x));
    } else {
        float z = exp(x);
        return z / (1.0 + z);
    }
}

float3 ComputeCov2D(float3 position, float3 log_scale, float4 rot)
{
    float tanFovX = _ProjectionParams.x;
    float tanFovY = _ProjectionParams.y;
    float focalX = _ProjectionParams.z;
    float focalY = _ProjectionParams.w;

    float[6] cov3d = ComputeCov3D(log_scale, rot);

    float4 t;
    t = mul(UNITY_MATRIX_MV, float4(position, 1.0));

    float 
    float limx = 1.3 * tan(tanFovX / 2);
    float limy = 1.3 * tan(tanFovY / 2);
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

    float4x4 W = transpose(_ObjectToWorld);

    float4x4 T = mul(W, J);

    float4x4 Vrk = float4x4(
        cov3d.x, cov3d.y, cov3d.z, 0.0,
        cov3d.y, cov3d.w, cov3d.z, 0.0,
        cov3d.z, cov3d.z, cov3d.z, 0.0,
        0.0, 0.0, 0.0, 0.0
    );

    float4x4 cov = mul(transpose(T), mul(transpose(Vrk), T));

    // Apply low-pass filter: every Gaussian should be at least
    // one pixel wide/high. Discard 3rd row and column.
    cov[0][0] += 0.3;
    cov[1][1] += 0.3;

    return float3(cov[0][0], cov[0][1], cov[1][1]);
}