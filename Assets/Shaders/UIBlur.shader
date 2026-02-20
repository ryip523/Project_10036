Shader "UI/Blur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurAmount ("Blur Amount", Range(0,1)) = 0

        // Unity UI stencil
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
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
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
            #pragma target 3.0

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
            float     _BlurAmount;

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

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float radius = _BlurAmount * 20.0;
                float2 texel = _MainTex_TexelSize.xy * radius;

                // 4-direction separable blur for uniform frosted glass look
                // Horizontal pass (7 taps)
                fixed4 hBlur = fixed4(0,0,0,0);
                hBlur += tex2D(_MainTex, uv + float2(-3, 0) * texel) * 0.0625;
                hBlur += tex2D(_MainTex, uv + float2(-2, 0) * texel) * 0.125;
                hBlur += tex2D(_MainTex, uv + float2(-1, 0) * texel) * 0.1875;
                hBlur += tex2D(_MainTex, uv)                         * 0.25;
                hBlur += tex2D(_MainTex, uv + float2( 1, 0) * texel) * 0.1875;
                hBlur += tex2D(_MainTex, uv + float2( 2, 0) * texel) * 0.125;
                hBlur += tex2D(_MainTex, uv + float2( 3, 0) * texel) * 0.0625;

                // Vertical pass (7 taps)
                fixed4 vBlur = fixed4(0,0,0,0);
                vBlur += tex2D(_MainTex, uv + float2(0, -3) * texel) * 0.0625;
                vBlur += tex2D(_MainTex, uv + float2(0, -2) * texel) * 0.125;
                vBlur += tex2D(_MainTex, uv + float2(0, -1) * texel) * 0.1875;
                vBlur += tex2D(_MainTex, uv)                         * 0.25;
                vBlur += tex2D(_MainTex, uv + float2(0,  1) * texel) * 0.1875;
                vBlur += tex2D(_MainTex, uv + float2(0,  2) * texel) * 0.125;
                vBlur += tex2D(_MainTex, uv + float2(0,  3) * texel) * 0.0625;

                // Diagonal passes for smoother coverage
                fixed4 d1Blur = fixed4(0,0,0,0);
                d1Blur += tex2D(_MainTex, uv + float2(-3, -3) * texel * 0.7) * 0.0625;
                d1Blur += tex2D(_MainTex, uv + float2(-2, -2) * texel * 0.7) * 0.125;
                d1Blur += tex2D(_MainTex, uv + float2(-1, -1) * texel * 0.7) * 0.1875;
                d1Blur += tex2D(_MainTex, uv)                                * 0.25;
                d1Blur += tex2D(_MainTex, uv + float2( 1,  1) * texel * 0.7) * 0.1875;
                d1Blur += tex2D(_MainTex, uv + float2( 2,  2) * texel * 0.7) * 0.125;
                d1Blur += tex2D(_MainTex, uv + float2( 3,  3) * texel * 0.7) * 0.0625;

                fixed4 d2Blur = fixed4(0,0,0,0);
                d2Blur += tex2D(_MainTex, uv + float2(-3,  3) * texel * 0.7) * 0.0625;
                d2Blur += tex2D(_MainTex, uv + float2(-2,  2) * texel * 0.7) * 0.125;
                d2Blur += tex2D(_MainTex, uv + float2(-1,  1) * texel * 0.7) * 0.1875;
                d2Blur += tex2D(_MainTex, uv)                                * 0.25;
                d2Blur += tex2D(_MainTex, uv + float2( 1, -1) * texel * 0.7) * 0.1875;
                d2Blur += tex2D(_MainTex, uv + float2( 2, -2) * texel * 0.7) * 0.125;
                d2Blur += tex2D(_MainTex, uv + float2( 3, -3) * texel * 0.7) * 0.0625;

                // Combine all directions equally
                fixed4 col = (hBlur + vBlur + d1Blur + d2Blur) * 0.25;

                // Frosted glass effect: slight desaturation + brightness lift
                float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                col.rgb = lerp(col.rgb, float3(lum, lum, lum), _BlurAmount * 0.2);
                col.rgb += _BlurAmount * 0.04;

                col *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
