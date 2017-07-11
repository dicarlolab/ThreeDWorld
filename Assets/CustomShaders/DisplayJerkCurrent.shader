Shader "DisplayJerkCurrent" {
    SubShader {
        Pass {

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            uniform float4x4 _0MVP;
            uniform float4x4 _3MV;
            uniform float4x4 _2MV;
    		uniform float4x4 _1MV;
    		uniform float4x4 _0MV;

            struct v2f {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = mul(_0MVP, v.vertex);

                // Transform from model to world to camera
                float4 _3Pos = mul(_3MV, v.vertex);
                float4 _2Pos = mul(_2MV, v.vertex);
                float4 _1Pos = mul(_1MV, v.vertex);
                float4 _0Pos = mul(_0MV, v.vertex);

                //float4 acc = (_0Pos - 2 * _1Pos + _2Pos) - (_1Pos - 2 * _2Pos + _3Pos);
                float4 jerk = _0Pos - 3 * _1Pos + 3 * _2Pos - _3Pos;

                float3 maxJerk = float3(2.0, 2.0, 2.0);

                //o.color = fixed4(normalize(fixed3(vel.xyz)) * 0.5 + 0.5, 
                //	clamp(length(vel.xyz) / maxVel, -1.0, 1.0) * 0.5 + 0.5);

                o.color = fixed4(jerk.x / maxJerk.x, jerk.y / maxJerk.y, jerk.z / maxJerk.z, 1) * 0.5 + 0.5;
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
