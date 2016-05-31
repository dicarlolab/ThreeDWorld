// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

#warning Upgrade NOTE: unity_Scale shader variable was removed; replaced 'unity_Scale.w' with '1.0'

Shader "Retro Lamps/Rim_dust"
{ 
Properties
{
	_MainTex ("Base (RGB)", 2D) = "white" {}
	_UpTex ("UpTexture", 2D) = "white" {} 
	_SideTex ("SideTexture", 2D) = "white" {}
	_BumpMap ("Normalmap", 2D) = "bump" {} 
	_UpBumpMap ("Up Normalmap", 2D) = "bump" {} 
	_UpTexTile ("UpTex Tile", Float) = 1
	_UpTexFocus ("UpTex Focus", Range (0, 10)) = 4 
	_UpTexIntensity ("UpTex Intensity", Range (0, 10)) = 5 
	_SideTexFocus ("SideTex Focus", Range (0, 10)) = 4 
	_SideTexIntensity ("SideTex Intensity", Range (0, 10)) = 5	
	_SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
	_Shininess ("Shininess", Range (0.03, 1)) = 0.078125
	_RimColor ("Rim Color", Color) = (0.26,0.19,0.16,0.0)
	_RimPower ("Rim Power", Range(0.5,8.0)) = 3.0 
} 

SubShader
{ 
	Tags { "RenderType"="Opaque" }
	LOD 400
	 
	CGPROGRAM
	#pragma surface surf BlinnPhong vertex:vert
	#pragma only_renderers d3d9 
	#pragma target 3.0
	
	sampler2D _MainTex; 
	sampler2D _UpTex;
	sampler2D _SideTex;
	sampler2D _BumpMap;
	sampler2D _UpBumpMap;
	float _UpTexTile;
	float _UpTexFocus; 
	float _UpTexIntensity; 
	float _SideTexFocus; 
	float _SideTexIntensity;	
	float _Shininess;
	float4 _RimColor;
	float _RimPower;
	
	struct Input
	{
		float2 uv_MainTex;
		float3 viewDir; 
	//	float3 normal_world;
		float3 worldPos;
		float3 TtoW0;
		float3 TtoW1;
		float3 TtoW2;
	};
	
	void vert(inout appdata_full v, out Input o)
	{
	//	UNITY_INITIALIZE_OUTPUT(Input, o);
	//	o.normal_world = normalize(mul(v.normal, (float3x3)_Object2World));
		
		TANGENT_SPACE_ROTATION;
		o.TtoW0 = mul(rotation, ((float3x3)unity_ObjectToWorld)[0].xyz) * 1.0;
		o.TtoW1 = mul(rotation, ((float3x3)unity_ObjectToWorld)[1].xyz) * 1.0;		
		o.TtoW2 = mul(rotation, ((float3x3)unity_ObjectToWorld)[2].xyz) * 1.0;	
	}
	
	void surf (Input IN, inout SurfaceOutput o)
	{ 
		float3 normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
		
		float3x3 TtoW = float3x3(IN.TtoW0, IN.TtoW1, IN.TtoW2);
		float3 normal_world = mul(TtoW, normal);
	
		float3 upDir = normalize(float3(0, 1, 0));
		float upDensity = dot(upDir, normal_world);
		float sideDensity = 1.0f - saturate(abs(upDensity));
		upDensity = pow(saturate(upDensity), _UpTexFocus); 
		upDensity = saturate(_UpTexIntensity * upDensity); 
		sideDensity = pow(saturate(sideDensity), _SideTexFocus);  
		sideDensity = saturate(_SideTexIntensity * sideDensity);

		float2 upTexUV = _UpTexTile * abs(IN.worldPos.xz);	

		fixed4 tex = tex2D(_MainTex, IN.uv_MainTex); 
		fixed4 upTex = tex2D(_UpTex, upTexUV); 
		fixed4 sideTex = tex2D(_SideTex, float2(0.5, 1) * IN.uv_MainTex); 
		tex = lerp(tex, upTex, upTex.a*upDensity); 
		tex = lerp(tex, sideTex, sideTex.a*sideDensity);
		
		float3 upNormal = UnpackNormal(tex2D(_UpBumpMap, upTexUV));
		normal = lerp(normal, upNormal, upTex.a*upDensity);

		o.Albedo = tex;
		o.Normal = normal;
		o.Gloss = tex.a;
		o.Alpha = tex.a;
		o.Specular = _Shininess;
		half rim = 1.0 - saturate(dot (normalize(IN.viewDir), o.Normal));
		o.Emission = _RimColor.rgb * pow (rim, _RimPower); 
		
		//o.Albedo = float3(0.0f);
		//o.Gloss = 0;
		//o.Emission = tex.rgb;
	}
	
	ENDCG 
}

FallBack "Specular"
}
