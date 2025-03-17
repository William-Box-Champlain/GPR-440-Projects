Shader "VFF/VectorFieldVisualization"
{
    Properties
    {
        _VelocityTex ("Velocity Texture", 2D) = "white" {}
        _ColorIntensity ("Color Intensity", Range(0, 2)) = 1
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
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
            
            sampler2D _VelocityTex;
            float _ColorIntensity;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            float3 HSVToRGB(float3 hsv)
            {
                // HSV to RGB conversion
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(hsv.xxx + K.xyz) * 6.0 - K.www);
                return hsv.z * lerp(K.xxx, saturate(p - K.xxx), hsv.y);
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // Sample velocity texture
                float2 velocity = tex2D(_VelocityTex, i.uv).xy;
                
                // Calculate direction angle (0-360 degrees)
                float angle = atan2(velocity.y, velocity.x) / (2 * 3.14159) + 0.5;
                
                // Calculate magnitude (0-1)
                float magnitude = length(velocity) * _ColorIntensity;
                
                // Convert to HSV color (hue based on direction, saturation and value based on magnitude)
                float3 hsv = float3(angle, saturate(magnitude * 0.5 + 0.5), saturate(magnitude * 0.7 + 0.3));
                float3 rgb = HSVToRGB(hsv);
                
                // Alpha based on magnitude
                float alpha = saturate(magnitude * 0.7 + 0.3);
                
                return float4(rgb, alpha);
            }
            ENDCG
        }
    }
}
