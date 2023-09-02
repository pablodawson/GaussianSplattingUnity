// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

Shader "Point Cloud/DebugShae"
{
    Properties
    {
        _Tint("Tint", Color) = (0.5, 0.5, 0.5, 1)
        _PointSize("Point Size", Float) = 0.05
        [Toggle] _Distance("Apply Distance", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM

            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma multi_compile_fog
            #pragma multi_compile _ UNITY_COLORSPACE_GAMMA
            #pragma multi_compile _ _DISTANCE_ON
            #pragma multi_compile _ _COMPUTE_BUFFER

            #include "UnityCG.cginc"
            #include "Common.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                half3 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_Position;
                half3 color : COLOR;
                half psize : PSIZE;
                UNITY_FOG_COORDS(0)
            };

            struct Gaussians
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

            StructuredBuffer<Gaussians> gaussiansBuffer;

            half4 _Tint;
            float4x4 _Transform;
            half _PointSize;

        #if _COMPUTE_BUFFER
            StructuredBuffer<float4> _PointBuffer;
        #endif

        #if _COMPUTE_BUFFER
            Varyings Vertex(uint vid : SV_VertexID)
        #else
            Varyings Vertex(Attributes input)
        #endif
            {
            #if _COMPUTE_BUFFER

                //Gaussians gaussian = gaussiansBuffer[vid];
                //float3 position = gaussian.Position;
                float4 pos = mul(_Transform, float4(0,0,0,1));
                //float4 pt = _PointBuffer[vid];
                //float4 pos = mul(_Transform, float4(pt.xyz, 1));
                half3 col = half3(1,1,1);
            #else
                float4 pos = input.position;
                half3 col = input.color;
            #endif

            #ifdef UNITY_COLORSPACE_GAMMA
                col *= _Tint.rgb * 2;
            #else
                col *= LinearToGammaSpace(_Tint.rgb) * 2;
                col = GammaToLinearSpace(col);
            #endif

                Varyings o;
                o.position = UnityObjectToClipPos(pos);
                o.color = col;
            #ifdef _DISTANCE_ON
                o.psize = _PointSize / o.position.w * _ScreenParams.y;
            #else
                o.psize = _PointSize;
            #endif
                UNITY_TRANSFER_FOG(o, o.position);
                return o;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 c = half4(input.color, _Tint.a);
                UNITY_APPLY_FOG(input.fogCoord, c);
                return c;
            }

            ENDCG
        }
    }
    CustomEditor "Pcx.PointMaterialInspector"
}
