// The post stack: bloom, tonemap, grade, vignette, grain.
//
// Written by hand rather than pulled in from com.unity.postprocessing, for the same reason
// the audio is synthesised and the textures are generated — this project has no imported
// assets, and a package that ships its own shaders and lens-dirt textures would be the
// first. It is also less code than it looks: three passes, none of them clever.
//
// It lives in Resources so Resources.Load can find it in a player build. Shader.Find only
// resolves shaders that are in Resources or listed under Always Included Shaders, and a
// post stack that silently disappears in a build is worse than no post stack at all.

Shader "ZombieHouse/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv  : TEXCOORD0;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }
    ENDCG

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        // ---- 0: bright pass -------------------------------------------------
        //
        // Everything brighter than the threshold, with a soft knee so a surface sitting
        // right on the line fades in rather than popping. Without the knee you get a hard
        // edge crawling across a wall as the torch flickers, which reads as a bug.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half _Threshold;
            half _Knee;

            half4 frag(v2f i) : SV_Target
            {
                half3 c = tex2D(_MainTex, i.uv).rgb;

                half brightness = max(c.r, max(c.g, c.b));

                half soft = brightness - _Threshold + _Knee;
                soft = clamp(soft, 0.0, 2.0 * _Knee);
                soft = soft * soft / (4.0 * _Knee + 0.0001);

                half contribution = max(soft, brightness - _Threshold);
                contribution /= max(brightness, 0.0001);

                return half4(c * contribution, 1.0);
            }
            ENDCG
        }

        // ---- 1: separable blur ----------------------------------------------
        //
        // A nine-tap gaussian collapsed into five samples by riding the bilinear filter —
        // the offsets are deliberately fractional so each fetch averages two taps. Run
        // once horizontally and once vertically per iteration; widening _BlurDirection
        // between iterations grows the kernel far more cheaply than more taps would.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float2 _BlurDirection;

            half4 frag(v2f i) : SV_Target
            {
                float2 step = _BlurDirection * _MainTex_TexelSize.xy;

                half4 sum = tex2D(_MainTex, i.uv) * 0.2270270270;
                sum += tex2D(_MainTex, i.uv + step * 1.3846153846) * 0.3162162162;
                sum += tex2D(_MainTex, i.uv - step * 1.3846153846) * 0.3162162162;
                sum += tex2D(_MainTex, i.uv + step * 3.2307692308) * 0.0702702703;
                sum += tex2D(_MainTex, i.uv - step * 3.2307692308) * 0.0702702703;

                return sum;
            }
            ENDCG
        }

        // ---- 2: composite ----------------------------------------------------
        //
        // Bloom in, then tonemap, then grade. The order matters: tonemapping after the
        // bloom is added is what stops a lit torch blowing out to a white disc, and it is
        // the whole reason the emissive colours in ProtoMaterials can stop being pushed
        // past 2.0 to be visible at all.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _BloomTex;

            half  _BloomIntensity;
            half  _Exposure;
            half  _Contrast;
            half  _Saturation;
            half3 _ColourFilter;
            half  _VignetteIntensity;
            half  _VignetteSmoothness;
            half  _GrainIntensity;
            float _GrainSeed;

            half4 frag(v2f i) : SV_Target
            {
                half3 c = tex2D(_MainTex, i.uv).rgb;
                c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity;

                c *= _Exposure;
                c *= _ColourFilter;

                // Narkowicz's ACES fit: cheap, and it rolls the highlights off instead of
                // clipping them flat, which is most of why a graded frame looks like film
                // and an ungraded one looks like a screenshot.
                c = (c * (2.51 * c + 0.03)) / (c * (2.43 * c + 0.59) + 0.14);
                c = saturate(c);

                c = saturate((c - 0.5) * _Contrast + 0.5);

                half luma = dot(c, half3(0.2126, 0.7152, 0.0722));
                c = lerp(half3(luma, luma, luma), c, _Saturation);

                // Vignette. Darkening the corners does more for "you are somewhere
                // enclosed and unsafe" than any amount of geometry.
                float2 edge = abs(i.uv - 0.5) * _VignetteIntensity;
                edge = pow(saturate(edge), _VignetteSmoothness);
                half falloff = pow(saturate(1.0 - dot(edge, edge)), _VignetteSmoothness);
                c *= falloff;

                // Grain, animated by the seed. Flat colours over large areas band badly in
                // the dark; a little noise breaks the bands up and reads as film.
                float noise = frac(sin(dot(i.uv + _GrainSeed, float2(12.9898, 78.233))) * 43758.5453);
                c += (noise - 0.5) * _GrainIntensity;

                return half4(saturate(c), 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
