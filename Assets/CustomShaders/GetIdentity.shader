Shader "Get Identity" {
	Properties {
		_idval ("Object Identity", Color) = (0, 0, 0, 0)
	}
    SubShader {
		Tags { 
		      "Queue" = "Overlay+1" 
		}
        Pass {

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

			float4 _idval;

            struct v2f {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
            	o.color = _idval;
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