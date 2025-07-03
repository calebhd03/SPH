Shader "SPH/ParticleInstanced"
{
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
        _ParticleScale ("Particle Scale", Float) = 0.1
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            StructuredBuffer<float3> _Positions;
            float _ParticleScale;
            float4 _Color;

            struct appdata {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 pos = _Positions[v.instanceID];
                float3 sphereVertex = UnityObjectToWorldPos(float3(0,0,0)); // Replace with sphere mesh vertex
                o.pos = UnityObjectToClipPos(float4(pos,1));
                o.color = _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}