Shader "DisplayVelocity" {
    SubShader {
        Pass {

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            uniform float4x4 _1MVP;
    		uniform float4x4 _1MV;
    		uniform float4x4 _0MV;

            struct v2f {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = mul(_1MVP, v.vertex);

                // Transform from model to world to camera
                float4 prevPos = mul(_1MV, v.vertex);
                float4 currPos = mul(_0MV, v.vertex);
                float4 vel = currPos - prevPos;

                float3 maxVel = float3(.1, .1, .1);

                //o.color = fixed4(normalize(fixed3(vel.xyz)) * 0.5 + 0.5, 
                //	clamp(length(vel.xyz) / maxVel, -1.0, 1.0) * 0.5 + 0.5);

                o.color = fixed4(vel.x / maxVel.x, vel.y / maxVel.y, vel.z / maxVel.z, 1) * 0.5 + 0.5;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG

        }
    }
}
