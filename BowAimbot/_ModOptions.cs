using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace BasAimbot
{
    public static class _ModOptions
    {

        // -- Bow Options --

        [ModOptionCategory("Bow", 1)]
        [ModOption("Enabled", "Enables/Disables the aimbot for Bows")]
        public static bool bowEnabled = true;

        [ModOptionCategory("Bow", 1)]
        [ModOptionFloatValues(0.1f, 10f, 0.1f)]
        [ModOption("Seeking Speed", interactionType = ModOption.InteractionType.Slider)]
        public static float bowSeekingSpeed = 1f;

        [ModOptionCategory("Bow", 1)]
        [ModOptionButton]
        [ModOption("Wall Bang", "Enables/Disables wall bang")]
        public static bool bowWallBang = false;


        //  If the target is a little bit away, it will make it so then when the arrow goes flying it'll suddenly speedup like half a second in and make a firework sound and effect (only will play effect if it's visible)
        [ModOptionCategory("Bow", 1)]
        [ModOptionButton]
        [ModOption("Firework mode")]
        public static bool bowFireworkMode = false; 

        // -- Throwing Star Options -- 

        [ModOptionCategory("Throwing Stars", 2)]
        [ModOption("Enabled", "Enables/Disables the aimbot for Throwing Stars")]
        public static bool starEnabled = true;

        [ModOptionCategory("Throwing Stars", 2)]
        [ModOptionFloatValues(0.1f, 10f, 0.1f)]
        [ModOption("Seeking Speed", interactionType = ModOption.InteractionType.Slider)]
        public static float starSeekingSpeed = 1f;

        [ModOptionCategory("Throwing Stars", 2)]
        [ModOptionButton]
        [ModOption("Wall Bang", "Enables/Disables wall bang")]
        public static bool starWallBang = false;



        [ModOptionCategory("Throwing Stars", 2)]
        [ModOptionButton]
        [ModOption("Firework mode")]
        public static bool starFireworkMode = false;
        
        // -- General Options --

        [ModOptionCategory("General", 3)]
        [ModOption(name = "Aim Part", tooltip = "Part it aims at", valueSourceName = nameof(AimPartValues))]
        public static string ragdollAimPart;

        [ModOptionCategory("General", 3)]
        [ModOptionIntValues(0, 360, 1)]
        [ModOptionSlider]
        [ModOption("Max Angle", tooltip = "This just means how far away from the target you can look/aim for it to hit, set to 360 for all around.")]
        public static int angle = 180;

        [ModOptionCategory("General", 3)]
        [ModOptionIntValues(0, 250, 1)]
        [ModOptionSlider]
        [ModOption("Max Distance", tooltip = "Max distance to the creature")]
        public static int maxDistanceToCreature = 100;



        internal static ModOptionString[] AimPartValues() => new[]
        {
            new ModOptionString("Closest Part", "Closest Part"),
            new ModOptionString("Head",         "Head"),
            new ModOptionString("Neck",         "Neck"),
            new ModOptionString("Chest",        "Chest"),
            new ModOptionString("Left Hand",    "Left Hand"),
            new ModOptionString("Right Hand",   "Right Hand"),
            new ModOptionString("Left Leg",     "Left Leg"),
            new ModOptionString("Right Leg",    "Right Leg"),
            new ModOptionString("Left Foot",    "Left Foot"),
            new ModOptionString("Right Foot",   "Right Foot"),
            new ModOptionString("Random",       "Random")
        };

        internal static ModOptionString[] _PlayEffectsBow() => new[]
        {
            new ModOptionString("None", "None"),
            new ModOptionString("Gravity", "Gravity"),
            new ModOptionString("Explosion", "Explosion"),
            new ModOptionString("Lightning Strike", "Lightning Strike")
        };

        internal static ModOptionString[] _PlayEffectsStar() => new[]
{
            new ModOptionString("None", "None"),
            new ModOptionString("Gravity", "Gravity"),
            new ModOptionString("Explosion", "Explosion"),
            new ModOptionString("Lightning Strike", "Lightning Strike")
        };


        public class AimPartDefinition
        {
            public RagdollPart.Type PartType;
            public Func<Transform, Creature, Vector3> GetPosition;
        }

        public static readonly Dictionary<string, AimPartDefinition> AimPartDefinitions = new Dictionary<string, AimPartDefinition> // The "Closest Part" and "Random" options are handled separately in the code, so they are not included in this dictionary.
        {
            ["Head"] = new AimPartDefinition { PartType = RagdollPart.Type.Head, GetPosition = (t, c) => c.centerEyes.transform.position },
            ["Neck"] = new AimPartDefinition { PartType = RagdollPart.Type.Neck, GetPosition = (t, c) => t.position },
            ["Chest"] = new AimPartDefinition { PartType = RagdollPart.Type.Torso, GetPosition = (t, c) => t.TransformPoint(-0.5f, 0f, 0f) },
            ["Left Hand"] = new AimPartDefinition { PartType = RagdollPart.Type.LeftHand, GetPosition = (t, c) => t.position },
            ["Right Hand"] = new AimPartDefinition { PartType = RagdollPart.Type.RightHand, GetPosition = (t, c) => t.position },
            ["Left Leg"] = new AimPartDefinition { PartType = RagdollPart.Type.LeftLeg, GetPosition = (t, c) => t.TransformPoint(-0.25f, 0f, 0f) },
            ["Right Leg"] = new AimPartDefinition { PartType = RagdollPart.Type.RightLeg, GetPosition = (t, c) => t.TransformPoint(-0.25f, 0f, 0f) },
            ["Left Foot"] = new AimPartDefinition { PartType = RagdollPart.Type.LeftFoot, GetPosition = (t, c) => t.position },
            ["Right Foot"] = new AimPartDefinition { PartType = RagdollPart.Type.RightFoot, GetPosition = (t, c) => t.position },
        };


    }
}
