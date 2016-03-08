Shader "Get Identity" {
	Properties {
		_idval ("Object Identity", Float) = 1.
	}
    SubShader {
        Pass {

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

			float _idval;

            struct v2f {
                float4 pos : SV_POSITION;
                fixed3 color : COLOR0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
            	float v0 = (_idval / 65356) / 255.;
            	float v1 = ((_idval % 65356) / 256) / 255.;
            	float v2 = ((_idval % 65356) % 256) / 255.;
                o.color = fixed3(v0, v1, v2);
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