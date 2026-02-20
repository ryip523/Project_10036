Shader "UI/PortraitOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width (px)", Float) = 2.5
        _OutlineAlpha ("Outline Alpha", Range(0,1)) = 0.6

        // Required by Unity UI masking / stencil system
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull     Off
        Lighting Off
        ZWrite   Off
        ZTest    [unity_GUIZTestMode]
        Blend    SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            float4    _MainTex_ST;
            fixed4    _Color;
            fixed4    _ClipRect;
            float     _OutlineWidth;
            float     _OutlineAlpha;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex        = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord      = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color         = v.color * _Color;
                return OUT;
            }

            // Sample alpha at an offset (clamped to [0,1] UV range)
            float sampleA(float2 uv, float2 offset)
            {
                return tex2D(_MainTex, clamp(uv + offset, 0.0, 1.0)).a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, IN.texcoord) * IN.color;

                // Two rings at different radii for a smooth "fading" falloff
                float2 d1 = _MainTex_TexelSize.xy * _OutlineWidth;
                float2 d2 = _MainTex_TexelSize.xy * _OutlineWidth * 2.0;

                // Inner ring – 8 samples
                float n1 = 0;
                n1 = max(n1, sampleA(IN.texcoord, float2( d1.x,    0)));
                n1 = max(n1, sampleA(IN.texcoord, float2(-d1.x,    0)));
                n1 = max(n1, sampleA(IN.texcoord, float2(    0,  d1.y)));
                n1 = max(n1, sampleA(IN.texcoord, float2(    0, -d1.y)));
                n1 = max(n1, sampleA(IN.texcoord, float2( d1.x,  d1.y)));
                n1 = max(n1, sampleA(IN.texcoord, float2(-d1.x,  d1.y)));
                n1 = max(n1, sampleA(IN.texcoord, float2( d1.x, -d1.y)));
                n1 = max(n1, sampleA(IN.texcoord, float2(-d1.x, -d1.y)));

                // Outer ring – 8 samples (contributes less → fades outward)
                float n2 = 0;
                n2 = max(n2, sampleA(IN.texcoord, float2( d2.x,    0)));
                n2 = max(n2, sampleA(IN.texcoord, float2(-d2.x,    0)));
                n2 = max(n2, sampleA(IN.texcoord, float2(    0,  d2.y)));
                n2 = max(n2, sampleA(IN.texcoord, float2(    0, -d2.y)));
                n2 = max(n2, sampleA(IN.texcoord, float2( d2.x,  d2.y)));
                n2 = max(n2, sampleA(IN.texcoord, float2(-d2.x,  d2.y)));
                n2 = max(n2, sampleA(IN.texcoord, float2( d2.x, -d2.y)));
                n2 = max(n2, sampleA(IN.texcoord, float2(-d2.x, -d2.y)));

                // Blend rings: inner = full weight, outer = half weight → soft falloff
                float neighborAlpha = (n1 * 1.0 + n2 * 0.5) / 1.5;

                // Outline only where sprite is absent; modulated by canvas alpha
                float outlineA = saturate(neighborAlpha - col.a) * _OutlineAlpha * IN.color.a;

                // Composite white outline behind the sprite
                float combinedA   = col.a + outlineA * (1.0 - col.a);
                float3 combinedRGB = col.rgb;
                if (combinedA > 0.001)
                    combinedRGB = (col.rgb * col.a + float3(1,1,1) * outlineA * (1.0 - col.a)) / combinedA;

                fixed4 result = fixed4(combinedRGB, combinedA);

                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif

                return result;
            }
            ENDCG
        }
    }
}
