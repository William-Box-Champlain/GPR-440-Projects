Shader "Hidden/ConservativeDownsample"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Level ("Source Mip Level", Float) = 0
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Level;

            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate source texel size based on mip level
                float2 sourceTexelSize = _MainTex_TexelSize.xy * pow(2, _Level);
                
                // Conservative downsampling - check 2x2 source area
                // A pixel is traversable (1) if ANY source pixel is traversable
                float2 sourceUV = i.uv;
                
                // Sample 4 pixels from source mip level
                float4 samples;
                samples.x = tex2Dlod(_MainTex, float4(sourceUV + float2(-sourceTexelSize.x, -sourceTexelSize.y) * 0.25, 0, _Level)).r;
                samples.y = tex2Dlod(_MainTex, float4(sourceUV + float2(sourceTexelSize.x, -sourceTexelSize.y) * 0.25, 0, _Level)).r;
                samples.z = tex2Dlod(_MainTex, float4(sourceUV + float2(-sourceTexelSize.x, sourceTexelSize.y) * 0.25, 0, _Level)).r;
                samples.w = tex2Dlod(_MainTex, float4(sourceUV + float2(sourceTexelSize.x, sourceTexelSize.y) * 0.25, 0, _Level)).r;
                
                // For conservative downsampling:
                // - If ANY source pixel is traversable (1), the result is traversable
                float traversable = max(max(samples.x, samples.y), max(samples.z, samples.w));
                
                // Return the result
                return traversable;
            }
            ENDCG
        }
    }
}
