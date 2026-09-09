using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>
    /// The one place that decides how a level's own lights behave.
    ///
    /// WHY THIS EXISTS. Every light every generator placed was created with
    /// <c>shadows = LightShadows.None</c> — 161 of them across eight levels, and the
    /// flashlight plus a directional moon in five levels were the only things in the
    /// game casting a shadow at all. Two consequences, and the second is the worse one:
    ///
    ///   Nothing was grounded. A zombie standing under a lamp with no shadow does not
    ///   sit in the room, it floats in front of it, and the eye reads that as fake long
    ///   before it gets as far as judging the model.
    ///
    ///   Light went through walls. An unshadowed point light ignores geometry entirely,
    ///   so the house's forty 16 m lamps were lighting each other's rooms through solid
    ///   plaster. Every interior was softly, evenly lit from all directions by lamps the
    ///   player could not see, which is the opposite of what a horror level wants and
    ///   was never anybody's decision — it is just what a light with shadows off does.
    ///
    /// WHAT DOES NOT GET THIS. Anything attached to a creature, a pickup or an NPC keeps
    /// its shadows off, and that is not an oversight. A shadow-casting point light is a
    /// cubemap — six shadow map renders — so a bear's two eye-glows made shadow-casting
    /// would cost twelve per bear, scaling with the horde. Those lights exist to be seen,
    /// not to light anything, and Test Shadows enforces that they stay that way.
    /// </summary>
    public static class LevelLighting
    {
        /// <summary>
        /// Hard rather than soft, deliberately. These are point lights, and a point light
        /// renders six shadow map faces; soft filtering multiplies the per-pixel cost of
        /// every one of them for a softness nobody reads at the range a room light is
        /// seen. The flashlight stays soft — it is one spot light, it is the light the
        /// player is actually looking along, and its moving shadows are most of the
        /// fright.
        /// </summary>
        public static void MakeRoomLight(Light light)
        {
            if (light == null) return;

            light.shadows = LightShadows.Hard;

            // Bias trades one artefact for the other and there is no setting that has
            // neither. Too little and a surface shadows itself in stripes; too much and
            // the shadow detaches from the feet casting it, which undoes the entire
            // reason for turning shadows on. These are tuned down from Unity's defaults
            // because that default assumes rooms much larger than a 2 m grid cell — at
            // this scale the default 0.05 lifts a walker's shadow visibly off the floor.
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.15f;
            light.shadowNearPlane = 0.1f;
        }

        /// <summary>
        /// Re-encodes an authored light level so it still means what it meant in gamma.
        ///
        /// Ambient and fog colours are authored as sRGB and Unity linearises them before
        /// using them. In gamma space it did not, so the number in the source WAS the
        /// light contribution. Switching to linear silently reinterprets every one of
        /// them: an ambient of 0.055 stops contributing 0.055 and starts contributing
        /// 0.0045, which is twelve times less. Eight levels would go black, and not
        /// because anybody decided they should.
        ///
        /// So the authored numbers stay meaningful and this converts them at the point of
        /// use: the value handed to Unity is the one whose linearisation equals what was
        /// written. Under gamma it is a no-op, which is what makes the switch reversible
        /// without touching a single level's palette.
        ///
        /// This is deliberately NOT applied to light intensity. A light's intensity is a
        /// scalar and is not colour-converted, and the rest of the difference between the
        /// two colour spaces is a redistribution rather than a scale - at full brightness
        /// they agree exactly, and in the falloff linear is actually the brighter of the
        /// two. There is no single number that compensates for that, which is why nothing
        /// here pretends there is one.
        /// </summary>
        public static Color AsAuthored(Color authored)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear) return authored;

            return new Color(
                Mathf.LinearToGammaSpace(authored.r),
                Mathf.LinearToGammaSpace(authored.g),
                Mathf.LinearToGammaSpace(authored.b),
                authored.a);
        }

        /// <summary>The scene's ambient and fog, authored in the units they always were.</summary>
        public static void SetAmbient(Color ambient) =>
            RenderSettings.ambientLight = AsAuthored(ambient);

        public static void SetFogColor(Color fog) =>
            RenderSettings.fogColor = AsAuthored(fog);
    }
}
