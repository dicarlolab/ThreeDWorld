Shader "DisplayDepth" {
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
                fixed4 color : COLOR0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
                float depth;
    			COMPUTE_EYEDEPTH(depth);
    			// Convert to "millimeters"
    			depth = min(depth * 1000.0, 65280); // 255*256
    			// Encode with base 256
    			float r = floor(depth / 256) / 255.0;
    			float g = floor(fmod(depth, 256)) / 255.0;
    			float b = floor(fmod(fmod(depth, 256), 1) * 256) / 255.0;
                o.color = fixed4(r,g,b,1);
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
