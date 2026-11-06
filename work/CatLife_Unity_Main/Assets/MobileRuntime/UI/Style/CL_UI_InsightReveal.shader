Shader "CatLife/UI/InsightReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Font atlas", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        _Elapsed ("Elapsed", Float) = 0
        _CharacterSeconds ("Character seconds", Float) = 0.09
        _StaggerSeconds ("Stagger seconds", Float) = 0.03
        _BlurPx ("Blur in design pixels", Float) = 6
        _SampleCount ("Gaussian samples", Float) = 64
        _Bezier ("Cubic Bezier", Vector) = (0.215,0.61,0.355,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float4 local:TEXCOORD0; float4 basisX:TEXCOORD1; float4 basisY:TEXCOORD2; float4 order:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 local:TEXCOORD0; float4 basisX:TEXCOORD1; float4 basisY:TEXCOORD2; float order:TEXCOORD3; float2 world:TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            float4 _ClipRect, _Bezier;
            float _Elapsed, _CharacterSeconds, _StaggerSeconds, _BlurPx, _SampleCount;
            float4 _GaussianOffsets[64];
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex); o.world=v.vertex.xy; o.color=v.color;
                o.local=v.local.xy; o.basisX=v.basisX; o.basisY=v.basisY; o.order=v.order.x; return o;
            }
            float curve(float x)
            {
                float t=x;
                [unroll] for(int n=0;n<5;n++)
                {
                    float u=1-t;
                    float bx=3*u*u*t*_Bezier.x+3*u*t*t*_Bezier.z+t*t*t;
                    float slope=3*u*u*_Bezier.x+6*u*t*(_Bezier.z-_Bezier.x)+3*t*t*(1-_Bezier.z);
                    t=saturate(t-(bx-x)/slope);
                }
                return 3*(1-t)*(1-t)*t*_Bezier.y+3*(1-t)*t*t*_Bezier.w+t*t*t;
            }
            float glyph(v2f i, float2 p)
            {
                float inGlyph=step(0,p.x)*step(p.x,1)*step(0,p.y)*step(p.y,1);
                float2 uv=i.basisX.xy+p.x*i.basisX.zw+p.y*i.basisY.xy;
                return tex2D(_MainTex,uv).a*inGlyph;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float phase=(_Elapsed-i.order*_StaggerSeconds)/_CharacterSeconds;
                if(i.order>=0 && phase<0) return 0;
                float sigma=i.order<0?0:_BlurPx*(1-curve(saturate(phase)));
                float alpha;
                if(sigma<0.12) alpha=glyph(i,i.local);
                else
                {
                    alpha=0;
                    // Keep the original 64-sample quality. Uniform loop bound prevents
                    // mobile drivers from expanding a large shader; offsets are prepared on CPU.
                    [loop] for(int n=0;n<(int)_SampleCount;n++)
                        alpha+=glyph(i,i.local+_GaussianOffsets[n].xy*sigma*i.basisY.zw);
                    alpha/=_SampleCount;
                }
                fixed4 color=i.color; color.a*=alpha;
                #ifdef UNITY_UI_CLIP_RECT
                color.a*=UnityGet2DClipping(i.world,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a-0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
