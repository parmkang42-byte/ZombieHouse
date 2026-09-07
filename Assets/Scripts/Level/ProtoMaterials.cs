using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>
    /// Blockout materials made in code so Stage 1 ships with no art dependencies.
    /// Probes for a URP shader first, then Built-in Standard, so the project keeps
    /// working if you later switch render pipeline.
    /// </summary>
    public static class ProtoMaterials
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Shader _shader;

        public static Shader LitShader
        {
            get
            {
                if (_shader == null)
                {
                    _shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (_shader == null) _shader = Shader.Find("Standard");
                    if (_shader == null) _shader = Shader.Find("Diffuse");
                }
                return _shader;
            }
        }

        /// <summary>
        /// Prefers a real .mat asset under Resources/ProtoMaterials so edits survive scene
        /// saves and builds; falls back to an in-memory material if the asset is missing.
        /// </summary>
        public static Material Get(string key, Color color, float smoothness = 0.1f, float metallic = 0f)
        {
            Material existing;
            if (Cache.TryGetValue(key, out existing) && existing != null) return existing;

            var asset = Resources.Load<Material>("ProtoMaterials/" + key);
            if (asset != null)
            {
                Cache[key] = asset;
                return asset;
            }

            var material = new Material(LitShader) { name = "Proto_" + key };
            SetColor(material, color);

            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);

            Cache[key] = material;
            return material;
        }

        /// <summary>Drops cached lookups so newly created .mat assets are picked up.</summary>
        public static void ClearCache()
        {
            Cache.Clear();
            _shader = null;
        }

        /// <summary>
        /// Switches a material's emission on. Emissive colour is what makes a bear's eyes
        /// read as lit from inside rather than merely painted red.
        ///
        /// The values these are given sit between about 1.5 and 2.8, which was originally
        /// compensation: there was no bloom, so the only way an emissive read as *lit*
        /// rather than *brightly painted* was to push it well past 1 and let it clip. With
        /// PostProcessStack in place they now bloom properly, and that same range is close
        /// to right for the opposite reason — it is comfortably above every level's bloom
        /// threshold. If something glares, these are the numbers to bring down, not the
        /// bloom intensity: turning the stack down dims every level, turning one emissive
        /// down fixes the one thing that is too bright.
        /// </summary>
        public static void MakeEmissive(Material material, Color emission)
        {
            if (material == null) return;

            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
            if (material.HasProperty("_EmissionMap")) material.SetFloat("_EmissiveExposureWeight", 0f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        /// <summary>A material that glows. Cached under its own key like every other.</summary>
        public static Material GetEmissive(string key, Color color, Color emission,
                                           float smoothness = 0.2f)
        {
            Material existing;
            if (Cache.TryGetValue(key, out existing) && existing != null) return existing;

            var asset = Resources.Load<Material>("ProtoMaterials/" + key);
            if (asset != null)
            {
                Cache[key] = asset;
                return asset;
            }

            // Get() would cache the non-emissive version under this key, so build it here
            // and let MakeEmissive finish it before it goes in the cache.
            var material = new Material(LitShader) { name = "Proto_" + key };
            SetColor(material, color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            MakeEmissive(material, emission);

            Cache[key] = material;
            return material;
        }

        public static void SetColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        // Shared palette for the house blockout.
        public static Material Floor => Get("floor", new Color(0.32f, 0.29f, 0.26f), 0.05f);

        /// <summary>A second floor tone, alternated with the first to read as boards.</summary>
        public static Material FloorAlt => Get("flooralt", new Color(0.27f, 0.24f, 0.21f), 0.07f);

        /// <summary>Skirting, dado and picture rails — the trim that makes a wall a room.</summary>
        public static Material Trim => Get("trim", new Color(0.20f, 0.16f, 0.13f), 0.12f);

        /// <summary>Panelling below the dado rail, a shade off the plaster above it.</summary>
        public static Material Wainscot => Get("wainscot", new Color(0.44f, 0.40f, 0.35f), 0.06f);
        public static Material Wall => Get("wall", new Color(0.58f, 0.55f, 0.50f), 0.03f);
        public static Material Ground => Get("ground", new Color(0.14f, 0.16f, 0.13f), 0.02f);
        public static Material ZombieFlesh => Get("zombie", new Color(0.36f, 0.47f, 0.31f), 0.15f);
        public static Material ZombieHead => Get("zombiehead", new Color(0.52f, 0.55f, 0.38f), 0.2f);
        /// Olive drab, for the gatling gun's belt boxes. This was the grenade crate's
        /// colour; the crates outlived the launcher, so the colour did too.
        /// Green, and unmistakably so. The player has to tell this from a yellow ammo box
        /// at a glance in a dark corridor, so the two are separated by hue rather than by
        /// shape — the old olive was close enough to the crates' surroundings that it read
        /// as scenery.
        public static Material BeltCrate => Get("beltcrate", new Color(0.13f, 0.46f, 0.19f), 0.30f, 0.25f);

        public static Material AmmoBox => Get("ammo", new Color(0.75f, 0.62f, 0.18f), 0.35f, 0.4f);
        public static Material Medkit => Get("medkit", new Color(0.78f, 0.16f, 0.18f), 0.35f);
        public static Material ExitGlow => Get("exit", new Color(0.2f, 0.85f, 0.45f), 0.5f);
        public static Material WeaponBody => Get("weapon", new Color(0.16f, 0.16f, 0.18f), 0.45f, 0.6f);

        /// Near-black hard chrome. The Desert Eagle is finished in this rather than in the
        /// lighter WeaponBody grey: a big handgun reads as menacing mostly by being dark
        /// and hard-edged, and a pale one just looks like a prop.
        public static Material GunBlack => Get("gunblack", new Color(0.055f, 0.058f, 0.065f), 0.62f, 0.85f);
        public static Material GunEdge => Get("gunedge", new Color(0.30f, 0.31f, 0.34f), 0.80f, 0.95f);

        /// Tritium night sights: three small dots that glow in a dark corridor.
        public static Material SightDot => GetEmissive("sightdot", new Color(0.35f, 0.85f, 0.45f),
                                                       new Color(0.6f, 2.1f, 0.9f), 0.5f);
        // Walker palette: ashen grey-green skin, filthy desaturated clothing, dried blood.
        public static Material Skin => Get("skin", new Color(0.55f, 0.56f, 0.48f), 0.10f);
        public static Material Shirt => Get("shirt", new Color(0.21f, 0.20f, 0.18f), 0.04f);
        public static Material Trousers => Get("trousers", new Color(0.17f, 0.18f, 0.21f), 0.04f);
        public static Material Gore => Get("gore", new Color(0.26f, 0.03f, 0.03f), 0.30f);
        public static Material Hair => Get("hair", new Color(0.11f, 0.09f, 0.08f), 0.06f);
        public static Material Blade => Get("blade", new Color(0.62f, 0.64f, 0.68f), 0.75f, 0.9f);

        /// The power cell: a scuffed industrial case with a warning stripe.
        public static Material CellCase => Get("cellcase", new Color(0.20f, 0.24f, 0.28f), 0.30f, 0.6f);
        public static Material CellStripe => GetEmissive("cellstripe", new Color(0.85f, 0.62f, 0.10f),
                                                         new Color(1.9f, 1.15f, 0.15f), 0.5f);
        public static Material MotorHousing => Get("motorhousing", new Color(0.26f, 0.27f, 0.30f), 0.45f, 0.8f);

        /// Dark glass with nothing behind it, and glass with a lamp behind it.
        public static Material WindowDark => Get("windowdark", new Color(0.05f, 0.06f, 0.08f), 0.72f);
        public static Material WindowLit => GetEmissive("windowlit", new Color(0.92f, 0.72f, 0.40f),
                                                        new Color(1.5f, 1.02f, 0.45f), 0.6f);
        public static Material Cactus => Get("cactus", new Color(0.24f, 0.31f, 0.19f), 0.10f);
        public static Material CactusSpine => Get("cactusspine", new Color(0.68f, 0.64f, 0.44f), 0.35f);

        // Egypt palette.
        public static Material Sandstone => Get("sandstone", new Color(0.62f, 0.52f, 0.35f), 0.04f);
        public static Material SandstoneAlt => Get("sandstonealt", new Color(0.54f, 0.44f, 0.29f), 0.04f);
        public static Material Sand => Get("sand", new Color(0.68f, 0.58f, 0.38f), 0.02f);
        public static Material Granite => Get("granite", new Color(0.26f, 0.22f, 0.22f), 0.22f);
        public static Material Gold => Get("gold", new Color(0.82f, 0.66f, 0.22f), 0.72f, 0.95f);
        public static Material Hieroglyph => Get("hieroglyph", new Color(0.40f, 0.32f, 0.20f), 0.06f);
        public static Material Bandage => Get("bandage", new Color(0.72f, 0.66f, 0.50f), 0.05f);
        public static Material BandageDark => Get("bandagedark", new Color(0.46f, 0.40f, 0.29f), 0.05f);

        /// The scarab: a hard iridescent shell, matt legs, and eyes lit from inside.
        public static Material ScarabShell => Get("scarabshell", new Color(0.10f, 0.16f, 0.13f), 0.78f, 0.7f);
        public static Material ScarabLimb => Get("scarablimb", new Color(0.13f, 0.12f, 0.10f), 0.20f);
        public static Material ScarabEye => GetEmissive("scarabeye", new Color(0.55f, 0.45f, 0.05f),
                                                        new Color(2.1f, 1.5f, 0.15f), 0.6f);

        // Jungle palette. Everything here is a green or a brown one shade off every other
        // green and brown, which is the point: in a canopy at dusk, contrast is the enemy.
        public static Material BarkPale => Get("barkpale", new Color(0.34f, 0.30f, 0.24f), 0.06f);
        public static Material Canopy => Get("canopy", new Color(0.07f, 0.17f, 0.08f), 0.10f);
        public static Material Frond => Get("frond", new Color(0.12f, 0.28f, 0.11f), 0.14f);
        public static Material FrondDark => Get("fronddark", new Color(0.06f, 0.14f, 0.07f), 0.12f);
        public static Material Undergrowth => Get("undergrowth", new Color(0.10f, 0.20f, 0.09f), 0.08f);
        public static Material JungleFloor => Get("junglefloor", new Color(0.16f, 0.14f, 0.10f), 0.04f);
        public static Material Mud => Get("mud", new Color(0.20f, 0.16f, 0.11f), 0.34f);
        public static Material Water => Get("water", new Color(0.08f, 0.16f, 0.15f), 0.92f, 0.2f);
        public static Material Vine => Get("vine", new Color(0.15f, 0.22f, 0.10f), 0.16f);
        public static Material Moss => Get("moss", new Color(0.13f, 0.24f, 0.12f), 0.05f);
        public static Material RuinStone => Get("ruinstone", new Color(0.30f, 0.31f, 0.27f), 0.10f);

        // ---- The Cormorant -------------------------------------------------
        // Wet rather than rusty. A ship abandoned long enough to rust through is a
        // wreck, and a wreck is scenery; this one went dark last week and everything
        // on it still works, which is a great deal worse.

        /// deck plating, wet underfoot
        public static Material DeckPlate => Get("deckplate", new Color(0.30f, 0.32f, 0.33f), 0.30f, 0.30f);
        /// and the alternating plate, for a seam
        public static Material DeckPlateAlt => Get("deckplatealt", new Color(0.26f, 0.28f, 0.30f), 0.30f, 0.30f);
        /// bulkheads and the hull itself
        public static Material HullPlate => Get("hullplate", new Color(0.22f, 0.26f, 0.29f), 0.24f, 0.40f);
        /// companionway treads: galvanised
        public static Material Grating => Get("grating", new Color(0.34f, 0.35f, 0.34f), 0.42f, 0.55f);
        /// a container that has crossed a lot of ocean
        public static Material ContainerRust => Get("containerrust", new Color(0.44f, 0.24f, 0.14f), 0.16f, 0.20f);
        /// and one that has not
        public static Material ContainerPaint => Get("containerpaint", new Color(0.18f, 0.34f, 0.42f), 0.22f, 0.15f);

        // The crew. Wet-weather gear is the only thing aboard with any colour in it, which
        // is exactly why they wear it: on a ship lit by a handful of working lamps the
        // silhouette has to do all the work the light cannot.

        /// deck oilskins: the yellow has gone green and stayed wet
        public static Material Oilskin => Get("oilskin", new Color(0.46f, 0.42f, 0.13f), 0.44f, 0.05f);
        /// the folds and the shoulders, where it is darker
        public static Material OilskinDark => Get("oilskindark", new Color(0.30f, 0.27f, 0.09f), 0.40f, 0.05f);
        /// a sou'wester, the same cloth given a harder shell
        public static Material SouWester => Get("souwester", new Color(0.41f, 0.37f, 0.11f), 0.55f, 0.05f);
        /// waders: black rubber to the knee
        public static Material Wader => Get("wader", new Color(0.07f, 0.07f, 0.08f), 0.36f, 0.05f);
        /// the life vest, faded but still the brightest thing on the ship
        public static Material LifeVest => Get("lifevest", new Color(0.62f, 0.27f, 0.06f), 0.14f, 0f);
        /// the officer's coat
        public static Material OfficerCoat => Get("officercoat", new Color(0.10f, 0.13f, 0.20f), 0.16f, 0.05f);
        /// cuff braid and buttons, tarnished
        public static Material OfficerBraid => Get("officerbraid", new Color(0.58f, 0.47f, 0.16f), 0.62f, 0.80f);
        /// the cap cover: white once, and the only pale thing below decks
        public static Material OfficerCap => Get("officercap", new Color(0.74f, 0.74f, 0.71f), 0.18f, 0f);

        // ---- Merryland ----------------------------------------------------
        // Sun-bleached rather than grimy. The park closed in the eighties and nobody
        // boarded it up; forty summers took the red out of everything and left the
        // shapes intact, which is far worse than rot. Cheerful colours gone chalky
        // read as abandoned in a way that dirt never does.

        /// Mister Squeak: black plush gone grey
        public static Material MascotFur => Get("mascotfur", new Color(0.16f, 0.16f, 0.19f), 0.06f, 0.00f);
        /// Missus Squeak, a shade warmer
        public static Material MascotFurAlt => Get("mascotfuralt", new Color(0.20f, 0.14f, 0.17f), 0.06f, 0.00f);
        /// Dilly Dog: tan, matted
        public static Material MascotFurDog => Get("mascotfurdog", new Color(0.44f, 0.31f, 0.16f), 0.05f, 0.00f);
        /// the cream muzzle and belly patch
        public static Material MascotFace => Get("mascotface", new Color(0.86f, 0.76f, 0.62f), 0.10f, 0.00f);
        /// a moulded plastic nose
        public static Material MascotNose => Get("mascotnose", new Color(0.09f, 0.08f, 0.09f), 0.35f, 0.00f);
        /// the painted smile
        public static Material MascotGrin => Get("mascotgrin", new Color(0.92f, 0.90f, 0.84f), 0.28f, 0.00f);
        /// the white of a painted eye
        public static Material MascotEye => Get("mascoteye", new Color(0.94f, 0.93f, 0.90f), 0.30f, 0.00f);
        /// and its pupil, always aimed at you
        public static Material MascotPupil => Get("mascotpupil", new Color(0.05f, 0.05f, 0.07f), 0.42f, 0.00f);
        /// bow, shorts, collar: faded circus red
        public static Material MascotBow => Get("mascotbow", new Color(0.52f, 0.13f, 0.16f), 0.14f, 0.00f);
        /// Dilly's crumpled felt hat
        public static Material MascotHat => Get("mascothat", new Color(0.24f, 0.34f, 0.30f), 0.08f, 0.00f);
        /// Dilly's waistcoat
        public static Material MascotVest => Get("mascotvest", new Color(0.30f, 0.36f, 0.48f), 0.10f, 0.00f);
        /// four-fingered gloves, greyed
        public static Material MascotGlove => Get("mascotglove", new Color(0.88f, 0.87f, 0.83f), 0.09f, 0.00f);
        /// boots two sizes too big
        public static Material MascotShoe => Get("mascotshoe", new Color(0.62f, 0.44f, 0.11f), 0.16f, 0.00f);
        /// a nylon wig
        public static Material PrincessHair => Get("princesshair", new Color(0.46f, 0.31f, 0.12f), 0.16f, 0.00f);
        /// greasepaint, run
        public static Material PrincessRouge => Get("princessrouge", new Color(0.60f, 0.16f, 0.20f), 0.12f, 0.00f);
        /// satin that was lilac once
        public static Material PrincessGown => Get("princessgown", new Color(0.44f, 0.40f, 0.56f), 0.22f, 0.00f);
        /// the hem, dragged through forty years
        public static Material PrincessGownTorn => Get("princessgowntorn", new Color(0.32f, 0.29f, 0.40f), 0.10f, 0.00f);
        /// a pageant sash
        public static Material PrincessSash => Get("princesssash", new Color(0.68f, 0.60f, 0.22f), 0.30f, 0.00f);
        /// cracked paving down the middle of it
        public static Material Midway => Get("midway", new Color(0.38f, 0.36f, 0.34f), 0.06f, 0.00f);
        /// the paler slabs, for a pattern
        public static Material MidwayAlt => Get("midwayalt", new Color(0.44f, 0.41f, 0.37f), 0.06f, 0.00f);
        /// lawns nobody has cut
        public static Material ParkGrass => Get("parkgrass", new Color(0.24f, 0.28f, 0.16f), 0.05f, 0.00f);
        /// big top canvas, sun-bleached
        public static Material TentCanvas => Get("tentcanvas", new Color(0.80f, 0.76f, 0.68f), 0.07f, 0.00f);
        /// and its red stripes
        public static Material TentStripe => Get("tentstripe", new Color(0.62f, 0.20f, 0.20f), 0.07f, 0.00f);
        /// carousel brass, still bright in places
        public static Material CarouselGilt => Get("carouselgilt", new Color(0.66f, 0.54f, 0.22f), 0.55f, 0.65f);
        /// the painted rounding boards
        public static Material CarouselPaint => Get("carouselpaint", new Color(0.72f, 0.66f, 0.58f), 0.20f, 0.00f);
        /// the wheel, rusting at the joints
        public static Material FerrisSteel => Get("ferrissteel", new Color(0.40f, 0.42f, 0.44f), 0.34f, 0.55f);
        /// the castle, which is painted plywood
        public static Material CastleStone => Get("castlestone", new Color(0.62f, 0.63f, 0.70f), 0.10f, 0.00f);
        /// its turret cones
        public static Material CastleRoof => Get("castleroof", new Color(0.28f, 0.34f, 0.46f), 0.18f, 0.00f);
        /// pennants, most of them gone
        public static Material Bunting => Get("bunting", new Color(0.70f, 0.52f, 0.20f), 0.10f, 0.00f);
        /// the games stalls
        public static Material StallWood => Get("stallwood", new Color(0.42f, 0.30f, 0.18f), 0.08f, 0.00f);
        /// their striped awnings
        public static Material StallAwning => Get("stallawning", new Color(0.54f, 0.46f, 0.30f), 0.09f, 0.00f);
        /// the teacups, chipped
        public static Material Teacup => Get("teacup", new Color(0.58f, 0.48f, 0.54f), 0.24f, 0.00f);
        /// the perimeter fence
        public static Material FenceRail => Get("fencerail", new Color(0.30f, 0.30f, 0.33f), 0.24f, 0.40f);

        /// The snake: banded scales with a wet sheen, and a flat unlit-looking eye.
        public static Material SnakeScale => Get("snakescale", new Color(0.13f, 0.20f, 0.11f), 0.62f, 0.25f);
        public static Material SnakeBand => Get("snakeband", new Color(0.30f, 0.24f, 0.06f), 0.58f, 0.25f);
        public static Material SnakeEye => GetEmissive("snakeeye", new Color(0.60f, 0.52f, 0.06f),
                                                       new Color(1.6f, 1.2f, 0.10f), 0.5f);

        /// The jaguar: tawny pelt, black rosettes, and eyes that catch what light there is.
        public static Material JaguarPelt => Get("jaguarpelt", new Color(0.42f, 0.31f, 0.14f), 0.08f);
        public static Material JaguarSpot => Get("jaguarspot", new Color(0.09f, 0.07f, 0.05f), 0.10f);
        public static Material JaguarEye => GetEmissive("jaguareye", new Color(0.68f, 0.62f, 0.20f),
                                                        new Color(2.2f, 1.9f, 0.45f), 0.55f);

        /// The monkeys: dark fur, bare face and hands.
        public static Material MonkeyFur => Get("monkeyfur", new Color(0.17f, 0.13f, 0.11f), 0.06f);
        public static Material MonkeyFace => Get("monkeyface", new Color(0.33f, 0.24f, 0.21f), 0.14f);

        /// Torch flame, for the sconces down a pyramid corridor.
        public static Material Flame => GetEmissive("flame", new Color(1f, 0.62f, 0.22f),
                                                    new Color(2.6f, 1.35f, 0.35f), 0.3f);

        // School palette.
        public static Material Cardigan => Get("cardigan", new Color(0.42f, 0.30f, 0.36f), 0.05f);
        public static Material StaffBadge => Get("staffbadge", new Color(0.86f, 0.84f, 0.78f), 0.30f);
        public static Material KidShirt => Get("kidshirt", new Color(0.24f, 0.46f, 0.68f), 0.06f);
        public static Material Backpack => Get("backpack", new Color(0.58f, 0.22f, 0.20f), 0.10f);

        /// Janitor's coveralls: dark navy work clothing, the darkest garment in the game.
        public static Material Coveralls => Get("coveralls", new Color(0.09f, 0.11f, 0.16f), 0.05f);
        public static Material MopHead => Get("mophead", new Color(0.52f, 0.50f, 0.44f), 0.08f);

        public static Material Linoleum => Get("linoleum", new Color(0.44f, 0.43f, 0.39f), 0.30f);
        public static Material LinoleumAlt => Get("linoleumalt", new Color(0.37f, 0.37f, 0.34f), 0.30f);
        public static Material SchoolWall => Get("schoolwall", new Color(0.55f, 0.54f, 0.46f), 0.05f);
        public static Material Locker => Get("locker", new Color(0.22f, 0.35f, 0.31f), 0.45f, 0.6f);
        /// A chalkboard: near-black with a green cast, and matt enough that it does not
        /// catch the strip lights the way the whiteboards do.
        public static Material Chalkboard => Get("chalkboard", new Color(0.09f, 0.14f, 0.11f), 0.06f);
        public static Material ChalkTray => Get("chalktray", new Color(0.42f, 0.33f, 0.22f), 0.12f);
        public static Material ChalkDust => Get("chalkdust", new Color(0.82f, 0.83f, 0.80f), 0.05f);

        public static Material Whiteboard => Get("whiteboard", new Color(0.88f, 0.88f, 0.85f), 0.55f);
        public static Material DeskTop => Get("desktop", new Color(0.68f, 0.55f, 0.36f), 0.20f);
        public static Material GymFloor => Get("gymfloor", new Color(0.60f, 0.44f, 0.25f), 0.35f);

        /// A live fluorescent tube. The dead ones use LinoleumAlt so they read as grey glass.
        public static Material Fluorescent => GetEmissive("fluorescent", new Color(0.90f, 0.94f, 0.98f),
                                                          new Color(1.7f, 1.85f, 2.0f), 0.4f);

        // Old West palette.
        public static Material HatLeather => Get("hatleather", new Color(0.19f, 0.14f, 0.10f), 0.12f);
        public static Material BootLeather => Get("bootleather", new Color(0.30f, 0.19f, 0.11f), 0.18f);
        public static Material Denim => Get("denim", new Color(0.20f, 0.26f, 0.36f), 0.05f);
        public static Material Adobe => Get("adobe", new Color(0.52f, 0.42f, 0.31f), 0.04f);
        public static Material Plank => Get("plank", new Color(0.36f, 0.26f, 0.17f), 0.08f);
        public static Material PlankPale => Get("plankpale", new Color(0.46f, 0.36f, 0.26f), 0.08f);
        public static Material Dust => Get("dust", new Color(0.33f, 0.27f, 0.20f), 0.02f);
        public static Material Mesa => Get("mesa", new Color(0.30f, 0.20f, 0.15f), 0.05f);

        /// The horse. Hide, mane and a shod hoof that catches the beam.
        public static Material HorseHide => Get("horsehide", new Color(0.17f, 0.14f, 0.13f), 0.07f);
        public static Material HorseMane => Get("horsemane", new Color(0.09f, 0.08f, 0.08f), 0.05f);
        public static Material HorseHoof => Get("horsehoof", new Color(0.55f, 0.57f, 0.60f), 0.80f, 0.95f);

        /// Bright pink, lit from inside: nothing else in the game is this colour.
        public static Material HorseEye => GetEmissive("horseeye", new Color(0.60f, 0.10f, 0.40f),
                                                       new Color(2.8f, 0.35f, 1.7f), 0.7f);

        /// Warm ochre. Nothing else in either level is this colour, which is the point:
        /// a survivor must never be mistaken for a walker, even at the edge of the beam.
        public static Material SurvivorCoat => Get("survivorcoat", new Color(0.72f, 0.46f, 0.16f), 0.10f);

        /// The lantern they are holding — the warmest light in the game.
        public static Material LanternGlass => GetEmissive("lantern", new Color(0.95f, 0.72f, 0.38f),
                                                           new Color(2.2f, 1.35f, 0.55f), 0.6f);

        /// Copper-topped and glossy, so a torch cell catches your own beam from a distance.
        public static Material Battery => Get("battery", new Color(0.62f, 0.42f, 0.16f), 0.60f, 0.7f);

        // Forest palette: everything desaturated and dark, lit by moonlight.
        public static Material ForestFloor => Get("forestfloor", new Color(0.10f, 0.12f, 0.08f), 0.03f);
        public static Material Bark => Get("bark", new Color(0.15f, 0.12f, 0.10f), 0.04f);
        public static Material Foliage => Get("foliage", new Color(0.07f, 0.13f, 0.08f), 0.05f);
        public static Material Rock => Get("rock", new Color(0.19f, 0.20f, 0.21f), 0.08f);
        public static Material Path => Get("path", new Color(0.17f, 0.15f, 0.12f), 0.03f);
        public static Material BearFur => Get("bearfur", new Color(0.13f, 0.11f, 0.10f), 0.06f);

        /// Wet, yellowed bone — glossy enough to catch a torch beam in the dark.
        public static Material BearTooth => Get("beartooth", new Color(0.82f, 0.79f, 0.66f), 0.55f);

        /// Blood red and lit from inside: the first thing you see of a bear in the trees.
        public static Material BearEye => GetEmissive("beareye", new Color(0.35f, 0.02f, 0.02f),
                                                      new Color(2.6f, 0.10f, 0.06f), 0.7f);

        /// <summary>Scope glass — see-through, so the sight does not block what you are aiming at.</summary>
        public static Material Glass
        {
            get
            {
                Material existing;
                if (Cache.TryGetValue("glass", out existing) && existing != null) return existing;

                var asset = Resources.Load<Material>("ProtoMaterials/glass");
                if (asset != null)
                {
                    Cache["glass"] = asset;
                    return asset;
                }

                var material = new Material(LitShader) { name = "Proto_glass" };
                SetColor(material, new Color(0.55f, 0.62f, 0.68f, 0.22f));
                MakeTransparent(material);
                Cache["glass"] = material;
                return material;
            }
        }

        /// <summary>
        /// Switches a material to alpha blending. The Built-in Standard shader needs its
        /// blend modes, keywords and render queue set by hand — setting the colour's alpha
        /// alone does nothing, which is the usual reason "transparent" materials come out
        /// solid. URP's Lit is handled too, via its own surface-type property.
        /// </summary>
        public static void MakeTransparent(Material material)
        {
            if (material == null) return;

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);   // URP: transparent
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);         // Standard: transparent

            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        public static Material Wood => Get("wood", new Color(0.30f, 0.20f, 0.13f), 0.12f);
        public static Material Fabric => Get("fabric", new Color(0.26f, 0.28f, 0.32f), 0.04f);
        public static Material Metal => Get("metal", new Color(0.42f, 0.44f, 0.47f), 0.55f, 0.7f);
        public static Material Linen => Get("linen", new Color(0.62f, 0.60f, 0.56f), 0.08f);
    }
}
