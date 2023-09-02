Shader "Custom/Splatting"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Functions.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR0;
                float2 uv: TEXCOORD0;
            };

            struct Gaussian
            {
                float3 Position;
                float3 Scale;
                float4 RotQuat;
                float Opacity;
                // Nasty
                float3 ShCoeffs1;
                float3 ShCoeffs2;
                float3 ShCoeffs3;
                float3 ShCoeffs4;
                float3 ShCoeffs5;
                float3 ShCoeffs6;
                float3 ShCoeffs7;
                float3 ShCoeffs8;
                float3 ShCoeffs9;
                float3 ShCoeffs10;
                float3 ShCoeffs11;
                float3 ShCoeffs12;
                float3 ShCoeffs13;
                float3 ShCoeffs14;
                float3 ShCoeffs15;
                float3 ShCoeffs16;
            };

            float2 quadVertices[6] = {
                float2(-1.0, -1.0),
                float2(-1.0, 1.0),
                float2(1.0, -1.0),
                float2(1.0, 1.0),
                float2(-1.0, 1.0),
                float2(1.0, -1.0)
            };
            
            
            StructuredBuffer<Gaussian> gaussiansBuffer;
            
            uniform uint _BaseVertexIndex;
            uniform float4x4 _ObjectToWorld;
            uniform float _NumInstances;

            v2f vert(uint vertexID: SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;

                uint pointIndex = vertexID / 6;
                uint quadIndex = vertexID % 6;
                float2 quadOffset = quadVertices[quadIndex];

                Gaussian gaussian = gaussiansBuffer[pointIndex];

                float3 cov2d = ComputeCov2D(gaussian.Position, gaussian.Scale, gaussian.RotQuat);
                float det = cov2d.x * cov2d.z - cov2d.y * cov2d.y;
                float det_inv = 1.0 / det;
                float3 conic = float3(cov2d.z * det_inv, -cov2d.y * det_inv, cov2d.x * det_inv);
                float mid = 0.5 * (cov2d.x + cov2d.z);
                float lambda_1 = mid + sqrt(max(0.1, mid * mid - det));
                float lambda_2 = mid - sqrt(max(0.1, mid * mid - det));
                float radius_px = ceil(3. * sqrt(max(lambda_1, lambda_2)));
                float2 radius_ndc = float2(
                    radius_px / (_ScreenParams.y),
                    radius_px / (_ScreenParams.x),
                );

                o.conic_and_opacity = float4(conic, sigmoid(gaussian.Opacity));

                float4 projPosition = UNITY_MATRIX_MV * float4(gaussian.Position, 1.0);
                projPosition = projPosition / projPosition.w;
                o.pos = float4(projPosition.xy + 2 * radius_ndc * quadOffset, projPosition.zw);
                o.color = compute_color_from_sh(_WorldSpaceCameraPos, gaussian.ShCoeffs1); // Full gaussian SH
                o.uv = radius_px * quadOffset;
                
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // we want the distance from the gaussian to the fragment while uv
                // is the reverse
                float2 d = -i.uv;
                float4 conic = i.conic_and_opacity.xyz;
                float power = -0.5 * (conic.x * d.x * d.x + conic.z * d.y * d.y) + conic.y * d.x * d.y;
                float opacity = i.conic_and_opacity.w;

                if (power > 0.0) {
                    discard;
                }

                float alpha = min(0.99, opacity * exp(power));

                return float4(i.color * alpha, alpha);
                
                //return i.color;
            }
            ENDCG
        }
    }
}