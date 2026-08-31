using UnityEngine;

namespace ZombieHouse.Fx
{
    /// <summary>
    /// Builds ParticleSystems in code. Emission is manual — the systems sit idle and the
    /// game calls Emit() on them, which is what you want for one-shot hits and gunfire.
    /// </summary>
    public static class ParticleFactory
    {
        public struct Spec
        {
            public Color StartColour;
            public Color EndColour;
            public float SizeMin;
            public float SizeMax;
            public float LifetimeMin;
            public float LifetimeMax;
            public float SpeedMin;
            public float SpeedMax;
            public float ConeAngle;
            public float Gravity;
            public float Drag;
            public int MaxParticles;
        }

        public static ParticleSystem Create(string name, Transform parent, Spec spec)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var particles = go.AddComponent<ParticleSystem>();

            // Stop before configuring: several modules refuse edits while playing.
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(spec.LifetimeMin, spec.LifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(spec.SpeedMin, spec.SpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(spec.SizeMin, spec.SizeMax);
            main.startColor = spec.StartColour;
            main.gravityModifier = spec.Gravity;
            main.maxParticles = Mathf.Max(8, spec.MaxParticles);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            // Manual emission only.
            var emission = particles.emission;
            emission.enabled = false;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = spec.ConeAngle;
            shape.radius = 0.01f;

            var colourOverLifetime = particles.colorOverLifetime;
            colourOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(spec.StartColour, 0f),
                    new GradientColorKey(spec.EndColour, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(spec.StartColour.a, 0f),
                    new GradientAlphaKey(spec.StartColour.a * 0.85f, 0.25f),
                    new GradientAlphaKey(0f, 1f)
                });
            colourOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.55f),
                    new Keyframe(0.35f, 1f),
                    new Keyframe(1f, 0.8f)));

            if (spec.Drag > 0f)
            {
                var limit = particles.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.dampen = spec.Drag;
                limit.limit = new ParticleSystem.MinMaxCurve(0.2f);
            }

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = ProtoTextures.ParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = -5f;

            return particles;
        }

        /// <summary>Fires a burst from a position, pointed along a direction.</summary>
        public static void Burst(ParticleSystem particles, Vector3 position, Vector3 direction, int count)
        {
            if (particles == null) return;

            particles.transform.position = position;
            if (direction.sqrMagnitude > 0.0001f)
                particles.transform.rotation = Quaternion.LookRotation(direction);

            particles.Emit(count);
        }

        // ---- presets --------------------------------------------------------

        public static Spec MuzzleSmoke => new Spec
        {
            StartColour = new Color(0.75f, 0.72f, 0.66f, 0.35f),
            EndColour = new Color(0.5f, 0.5f, 0.5f, 0f),
            SizeMin = 0.06f, SizeMax = 0.16f,
            LifetimeMin = 0.35f, LifetimeMax = 0.7f,
            SpeedMin = 0.6f, SpeedMax = 1.8f,
            ConeAngle = 14f,
            Gravity = -0.05f,
            Drag = 1.6f,
            MaxParticles = 120
        };

        public static Spec MuzzleSparks => new Spec
        {
            StartColour = new Color(1f, 0.82f, 0.45f, 0.95f),
            EndColour = new Color(1f, 0.35f, 0.1f, 0f),
            SizeMin = 0.015f, SizeMax = 0.045f,
            LifetimeMin = 0.05f, LifetimeMax = 0.14f,
            SpeedMin = 4f, SpeedMax = 10f,
            ConeAngle = 22f,
            Gravity = 0.2f,
            Drag = 0.5f,
            MaxParticles = 80
        };

        public static Spec WallDust => new Spec
        {
            StartColour = new Color(0.78f, 0.74f, 0.66f, 0.6f),
            EndColour = new Color(0.6f, 0.58f, 0.52f, 0f),
            SizeMin = 0.04f, SizeMax = 0.14f,
            LifetimeMin = 0.25f, LifetimeMax = 0.6f,
            SpeedMin = 1.5f, SpeedMax = 4f,
            ConeAngle = 38f,
            Gravity = 0.35f,
            Drag = 1.2f,
            MaxParticles = 200
        };

        public static Spec BloodMist => new Spec
        {
            StartColour = new Color(0.58f, 0.03f, 0.03f, 0.92f),
            EndColour = new Color(0.22f, 0.01f, 0.01f, 0f),
            SizeMin = 0.05f, SizeMax = 0.22f,
            LifetimeMin = 0.4f, LifetimeMax = 1.1f,
            SpeedMin = 1.8f, SpeedMax = 6.5f,
            ConeAngle = 46f,
            Gravity = 1.15f,          // heavier droplets arc down and hit the floor
            Drag = 0.55f,
            MaxParticles = 900
        };
    }
}
