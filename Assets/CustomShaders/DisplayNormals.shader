Shader "Tutorial/Display Normals" {
//	Properties {
//		_NormalMap ("Bumpmap", 2D) = "bump" {}
//	}
    SubShader {
        Pass {

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f {
                float4 pos : SV_POSITION;
                fixed3 color : COLOR0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
                float4 tmp = float4(v.normal, 1);
                tmp = mul (UNITY_MATRIX_MVP, tmp) * 0.5 + 0.5;
                //o.color = (float3)tmp;
                o.color = fixed3(tmp[0], tmp[1], tmp[2]);
                // o.color = v.normal * 0.5 + 0.5;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4 (i.color, 1);
            }
            ENDCG

        }
    }
}