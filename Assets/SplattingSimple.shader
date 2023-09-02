Shader "Custom/Splatting"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
// Upgrade NOTE: excluded shader from DX11 because it uses wrong array syntax (type[size] name)
#pragma exclude_renderers d3d11
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

            static const float2 quadVertices[6] = {
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

            v2f vert(uint vertexID: SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;

                uint pointIndex = vertexID / 6;
                uint quadIndex = vertexID % 6;
                float2 quadOffset = quadVertices[quadIndex];
                
                Gaussian gaussian = gaussiansBuffer[pointIndex];

                float3 pos = gaussian.Position;

                float4 wpos = mul(_ObjectToWorld, float4(pos, 1.0f));

                o.pos = mul(UNITY_MATRIX_VP, wpos);
                o.color = float4(1,1,1,1);
                
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return i.color;            
            }
            ENDCG
        }
    }
}