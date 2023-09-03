Shader "Custom/SplattingDeg3"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Functions.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 color : COLOR0;
                float2 uv: TEXCOORD0;
                float4 conic_and_opacity : TEXCOORD1;
            };

            static const float2 quadVertices[6] = {
                float2(-1.0, -1.0),
                float2(-1.0, 1.0),
                float2(1.0, -1.0),
                float2(1.0, -1.0),
                float2(-1.0, 1.0),
                float2(1.0, 1.0)
            };
            
            StructuredBuffer<float3> Position;
            StructuredBuffer<float3> Scale;
            StructuredBuffer<float4> RotQuat;
            StructuredBuffer<float> Opacity;
            StructuredBuffer<float3> ShCoeffs;
            
            uniform uint _BaseVertexIndex;
            uniform float4x4 _ObjectToWorld;

            float focalX;
            float focalY;
            float tanFovX;
            float tanFovY;
            
            v2f vert(uint vertexID: SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;
                
                uint pointIndex = vertexID / 6;
                uint quadIndex = vertexID % 6;
                float2 quadOffset = quadVertices[quadIndex];
                
                float3 pos = Position[pointIndex];
                float4 rotQuat = RotQuat[pointIndex];
                float3 scale = Scale[pointIndex];
                float opacity = Opacity[pointIndex];


                float3 cov2d = ComputeCov2D(pos, scale, rotQuat, focalX, focalY, tanFovX, tanFovY); //revisar

                float det = cov2d.x * cov2d.z - cov2d.y * cov2d.y;

                float det_inv = 1.0 / det;
                float3 conic = float3(cov2d.z * det_inv, -cov2d.y * det_inv, cov2d.x * det_inv);
                float mid = 0.5 * (cov2d.x + cov2d.z);
                float lambda_1 = mid + sqrt(max(0.1, mid * mid - det));
                float lambda_2 = mid - sqrt(max(0.1, mid * mid - det));
                float radius_px = ceil(3. * sqrt(max(lambda_1, lambda_2)));
                
                float2 radius_ndc = float2(radius_px / (_ScreenParams.y), radius_px / (_ScreenParams.x)); //

                o.conic_and_opacity = float4(conic, sigmoid(opacity));
                
                float4 projPosition = mul(UNITY_MATRIX_V, float4(pos,1));
                projPosition = projPosition / projPosition.w;
                
                o.pos = mul(UNITY_MATRIX_P, float4(projPosition.xy + 2 * radius_ndc * quadOffset, projPosition.zw));

                uint shCount = 16;

                float3 _shCoeffs[16];

                for (uint i = 0; i < shCount; i++)
                {
                    _shCoeffs[i] = ShCoeffs[pointIndex * shCount + i];
                }

                float3 cam_position = (float3)(_WorldSpaceCameraPos);

                float3 color = ComputeColorFromSH(pos, _shCoeffs); //revisar

                o.color = color;

                o.uv = radius_px * quadOffset;
                 
                return o;

            }

            float4 frag(v2f i) : SV_Target
            {
                // we want the distance from the gaussian to the fragment while uv
                // is the reverse
                float2 d = -i.uv;
                float3 conic = i.conic_and_opacity.xyz;
                float power = -0.5 * (conic.x * d.x * d.x + conic.z * d.y * d.y) + conic.y * d.x * d.y;
                float opacity = i.conic_and_opacity.w;

                if (power > 0.0) {
                    discard;
                }

                float alpha = min(0.99, opacity * exp(power));

                return float4(i.color * alpha, alpha);
            }
            ENDCG
        }
    }
}