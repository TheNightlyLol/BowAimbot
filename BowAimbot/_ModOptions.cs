using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace BowAimbot
{
    public static class _ModOptions
    {
        public static string lastShotArrowId;
        public static List<string> excludedArrows = new List<string>();
        public static ExcludedArrow arrows;

        // ── Mod Options ─────────────────────────────────────────────────────────—  

        [ModOption("Enabled", "Enables/Disables the Mod")]
        public static bool enabled = true;

        [ModOptionFloatValues(0.1f, 10f, 0.1f)]
        [ModOptionCategory("Settings", 1)]
        [ModOption("Seeking Speed", interactionType = ModOption.InteractionType.Slider, category = "Settings")]
        public static float seekingSpeed = 1f;

        [ModOption("Wall Bang", "Enables/Disables wall bang", category = "Settings")]
        public static bool wallBang = false;

        [ModOptionIntValues(0, 180, 1)]
        [ModOptionSlider]
        [ModOption("Max Angle", tooltip = "Max angle away from target part", category = "Settings")]
        public static int angle = 15;

        [ModOptionIntValues(0, 250, 1)]
        [ModOptionSlider]
        [ModOption("Max Distance", tooltip = "Max distance to the creature", category = "Settings")]
        public static int maxDistanceToCreature = 100;

        [ModOption(name = "Aim Part", tooltip = "Part it aims at", valueSourceName = nameof(AimPartValues), category = "Settings")]
        public static string ragdollAimPart;

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

        [ModOptionButton]
        [ModOption("Exclude Last Shot Arrow", valueSourceName = nameof(ExcludeButtonLabel), category = "Exclusion")]
        public static void ExcludeArrow(string _)
        {
            if (lastShotArrowId != null && !excludedArrows.Contains(lastShotArrowId))
            {
                excludedArrows.Add(lastShotArrowId);
                MainScript.Save();
            }
        }

        [ModOptionButton]
        [ModOption("Unexclude Last Shot Arrow", "Unexclude the last shot arrow", valueSourceName = nameof(UnexcludeButtonLabel), category = "Exclusion")]
        public static void UnexcludeArrow(string _)
        {
            if (_ModOptions.lastShotArrowId != null && _ModOptions.excludedArrows.Contains(_ModOptions.lastShotArrowId))
            {
                _ModOptions.excludedArrows.Remove(_ModOptions.lastShotArrowId);
                MainScript.Save();
            }
        }

        internal static ModOptionString[] ExcludeButtonLabel() => new[] { new ModOptionString("Exclude Arrow", "Exclude Arrow") };
        internal static ModOptionString[] UnexcludeButtonLabel() => new[] { new ModOptionString("Unexclude Arrow", "Unexclude Arrow") };


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

        // ── End of Mod Options ─────────────────────────────────────────────────
    }
}
