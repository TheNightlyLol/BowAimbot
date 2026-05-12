using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThunderRoad;
using UnityEngine;
namespace BowAimbot
{
    public class ExcludedArrow
    {
        public List<string> excludedArrowsIds = new List<string>();
    }

    public class MainScript : ThunderScript
    {
        private const float ArrowSpeed = 30f;
        private const float ArrivalDistSqr = 0.15f;
        private const float MaxWallBangDist = 30f;

        private Creature targetCreature;
        private float targetDistance = Mathf.Infinity;



        public override void ScriptLoaded(ModManager.ModData modData)
        {
            base.ScriptLoaded(modData); // Calls the base class's ScriptLoaded method, which performs any necessary initialization for the mod.
            EventManager.OnBowFireEvent += OnBowFire; // Subscribe to the bow fire event to start seeking when an arrow is fired
            EventManager.onLevelLoad += OnLevelLoad; // Subscribe to the level load event to load the excluded arrows from the save file
        }

        private void OnLevelLoad(LevelData levelData, LevelData.Mode mode, EventTime eventTime)
        {
            GameManager.local.StartCoroutine(Load());
        }

        private void OnBowFire(RagdollHand hand, BowString bowString, Item arrow) // RagdollHand is the type, hand is the object. BowString is the type, bowString is the object. Item is the type, arrow is the object.
        {
            _ModOptions.lastShotArrowId = arrow.itemId; // Stores the ID of the last shot arrow. 

            if (!_ModOptions.enabled) return; // If the mod isn't enabled don't do anything. 
            if (_ModOptions.excludedArrows.Contains(arrow.itemId)) return; // Don't do anything if the arrow is excluded. 
            if (arrow.lastHandler.creature != Player.currentCreature) return; // Only seek if the arrow was shot by the player

            GameManager.local.StartCoroutine(SeekRoutine(arrow)); // Start the seeking coroutine for the arrow
            // What is a coroutine? It's a function that can pause its execution and return control to Unity, but then continue where it left off on the following frame. This is useful for things that need to happen over time, like seeking towards a target.
            arrow.OnFlyEndEvent += OnArrowFlyEnd; // Subscribe to the fly end event to stop seeking when the arrow stops flying
            arrow.mainCollisionHandler.OnCollisionStartEvent += OnArrowCollision; // Subscribe to the collision event to stop seeking when the arrow collides with something

            foreach (Damager damager in arrow.mainCollisionHandler.damagers) // Damager is: a component that handles dealing damage to creatures. We subscribe to its penetrate event to stop seeking when the arrow penetrates a creature.
                damager.OnPenetrateEvent += OnArrowPenetrate; // Subscribe to the penetrate event to stop seeking when the arrow penetrates a creature
        }


        private void OnArrowFlyEnd(Item arrow) // Called when the arrow stops flying, either because it hit something or because it reached its max range. Stop seeking in either case.
        {
            StopSeeking(arrow); // Stop seeking when the arrow stops flying
            arrow.OnFlyEndEvent -= OnArrowFlyEnd; // Unsubscribe from the fly end event
            arrow.mainCollisionHandler.OnCollisionStartEvent -= OnArrowCollision; // Unsubscribe from the collision event
            foreach (Damager damager in arrow.mainCollisionHandler.damagers) // Unsubscribe from the penetrate event for each damager
                damager.OnPenetrateEvent -= OnArrowPenetrate; // Unsubscribe from the penetrate event
        }

        private void OnArrowCollision(CollisionInstance collision) // Called when the arrow collides with something. Stop seeking in this case, but don't unsubscribe from the fly end event because the arrow might still be flying if it hit a creature and didn't penetrate.
        {
            Item arrow = collision.sourceColliderGroup.collisionHandler.item; // Get the arrow that collided
            StopSeeking(arrow); // Stop seeking when the arrow collides with something
            arrow.OnFlyEndEvent -= OnArrowFlyEnd; // Unsubscribe from the fly end event
            arrow.mainCollisionHandler.OnCollisionStartEvent -= OnArrowCollision; // Unsubscribe from the collision event
            foreach (Damager damager in arrow.mainCollisionHandler.damagers) // Unsubscribe from the penetrate event for each damager
                damager.OnPenetrateEvent -= OnArrowPenetrate; // Unsubscribe from the penetrate event
        }

        private void OnArrowPenetrate(Damager damager, CollisionInstance collision, EventTime eventTime) // Called when the arrow penetrates a creature. Stop seeking in this case, but don't unsubscribe from the fly end event or the collision event because the arrow might still be flying if it penetrated a creature and didn't hit anything else.
        {
            if (eventTime == EventTime.OnStart) return; // We only want to stop seeking after the penetrate event has finished processing, because the creature might still be alive and we want the arrow to continue seeking until it stops flying or collides with something. If we stop seeking at the start of the penetrate event, the arrow will stop seeking as soon as it penetrates a creature, even if the creature is still alive and the arrow is still flying.
            Item arrow = damager.collisionHandler.item; // Get the arrow that penetrated
            StopSeeking(arrow); // Stop seeking when the arrow penetrates a creature
            arrow.OnFlyEndEvent -= OnArrowFlyEnd; // Unsubscribe from the fly end event
            arrow.mainCollisionHandler.OnCollisionStartEvent -= OnArrowCollision; // Unsubscribe from the collision event
            damager.OnPenetrateEvent -= OnArrowPenetrate; // Unsubscribe from the penetrate event
        }

        private void StopSeeking(Item arrow) // Stop seeking by clearing the target and resetting the arrow's collision detection, and stopping the seeking coroutine if it's still running.
        {
            targetCreature = null; // Clear the target creature
            targetDistance = Mathf.Infinity; // Reset the target distance
            arrow.physicBody.rigidBody.detectCollisions = true; // Re-enable collision detection for the arrow in case it was disabled for wall banging
            GameManager.local.StopCoroutine(SeekRoutine(arrow)); // Stop the seeking coroutine for the arrow. This is safe to call even if the coroutine has already finished, because StopCoroutine will do nothing if the coroutine is not running.
        }

        // ── Seeking Coroutine ─────────────────────────────────────────────────────

        private IEnumerator SeekRoutine(Item arrow) // The main seeking coroutine that runs every frame while the arrow is flying and has a valid target. It updates the arrow's velocity to steer it towards the target creature's aim part, while also checking for line of sight and max distance.
        {
            if (!TryFindTarget(arrow, out Transform targetTransform)) // Try to find a target creature and aim part for the arrow. If we can't find one, stop the coroutine.
                yield break; // yield break means stop the coroutine.

            while (arrow != null && targetCreature != null // While the arrow and target creature are still valid, and the target creature is not killed, and the target distance is within the max distance for seeking, keep updating the arrow's velocity to steer towards the target.
                && !targetCreature.isKilled
                && targetDistance <= _ModOptions.maxDistanceToCreature)
            {
                targetDistance = Vector3.Distance( // Update the target distance every frame in case the creature is moving or the arrow is getting closer or farther away
                    targetCreature.ragdoll.rootPart.transform.position,
                    arrow.transform.position);

                Vector3 targetPos = GetTargetPosition(targetTransform, targetCreature); // Get the target position based on the selected aim part. This is where the arrow will try to steer towards.
                Vector3 toTarget = targetPos - arrow.transform.position; // Get the vector from the arrow to the target position. We will use this to calculate the direction to steer towards and to check if we have arrived at the target.

                if (toTarget.sqrMagnitude > ArrivalDistSqr) // If we haven't arrived at the target position yet, keep steering towards it. We use sqrMagnitude and ArrivalDistSqr to avoid the costly square root operation of Vector3.Distance.
                { // sqrMagnitude is the squared length of the vector, and ArrivalDistSqr is the squared distance threshold for considering the arrow to have arrived at the target. This is an optimization to avoid calculating the actual distance with a square root, which is more expensive.
                    Vector3 dir = toTarget.normalized; // Get the normalized direction from the arrow to the target position. This is the direction we want to steer towards.
                    // What does Normalized do? It takes a vector and returns a new vector that points in the same direction but has a length of 1. This is useful for getting the direction without affecting the speed when we multiply it by the arrow speed.
                    if (_ModOptions.wallBang)
                    {
                        arrow.physicBody.rigidBody.detectCollisions = false; // Disable collision detection for the arrow to allow it to pass through walls and other obstacles.
                        arrow.physicBody.velocity = dir * ArrowSpeed * _ModOptions.seekingSpeed; // Set the arrow's velocity to steer towards the target position, multiplied by the seeking speed multiplier from the mod options. This will make the arrow fly towards the target position even if there are walls in the way.
                    }
                    else if (HasLineOfSight(arrow, targetPos))
                    {
                        arrow.physicBody.velocity = dir * ArrowSpeed * _ModOptions.seekingSpeed; // Set the arrow's velocity to steer towards the target position, multiplied by the seeking speed multiplier from the mod options. This will make the arrow fly towards the target position only if there is a clear line of sight.
                    }
                }
                else
                {
                    StopSeeking(arrow); // If we have arrived at the target position, stop seeking. This will prevent the arrow from jittering around the target position and will allow it to hit the target creature more reliably.
                    yield break; // Stop the coroutine since we have arrived at the target position.
                }

                yield return null; // Wait for the next frame before continuing the loop. This allows the arrow's velocity to be updated every frame while it's flying towards the target.
            }

            StopSeeking(arrow); // If we exit the loop because the arrow or target creature is no longer valid, or the target creature is killed, or we exceeded the max distance, stop seeking to clean up and prevent any potential issues with a lost target.
        }

        private bool TryFindTarget(Item arrow, out Transform targetTransform) // This function tries to find the closest valid target creature and aim part for the arrow. It checks all active creatures in the scene, and for each creature, it checks the angle between the arrow's forward direction and the direction to the creature's aim part. If the angle is within the max angle specified in the mod options, and it's closer than any previously found target, it sets that creature as the target. It returns true if a valid target was found, and false otherwise.
        {
            targetTransform = null; // Initialize the output parameter for the target transform to null. This will be set to the aim part's transform of the target creature if a valid target is found.
            float closestAngle = Mathf.Infinity;

            foreach (Creature creature in Creature.allActive) // Iterate through all active creatures in the scene to find the closest valid target.
            {
                if (creature == null || creature.isKilled || creature.isPlayer) continue;

                RagdollPart aimPart = GetAimPart(arrow, creature); // Get the aim part of the creature based on the mod options.
                if (aimPart == null) continue; // If no valid aim part is found, skip this creature.

                float angleToCreature = Vector3.Angle( // Calculate the angle between the arrow's forward direction and the direction from the arrow to the creature's aim part. This will be used to determine if the creature is within the seeking angle specified in the mod options.
                    aimPart.transform.position - arrow.transform.position, // Get the direction vector from the arrow to the creature's aim part.
                    arrow.flyDirRef.forward); // Get the forward direction of the arrow, which is the direction it's currently flying towards.

                if (angleToCreature >= closestAngle || angleToCreature > _ModOptions.angle) continue; // If the angle to this creature is greater than the closest angle found so far, or if it's greater than the max angle specified in the mod options, skip this creature.

                closestAngle = angleToCreature; // Update the closest angle to this creature's angle, since it's the best candidate so far.
                targetCreature = creature; // Set the target creature to this creature, since it's the best candidate so far.
                targetDistance = Vector3.Distance( // Calculate the distance from the arrow to the target creature's root part. 
                    creature.ragdoll.rootPart.transform.position,
                    arrow.transform.position);
                targetTransform = aimPart.transform; // Set the target transform to the transform of the aim part of the target creature. This is where the arrow will try to steer towards in the seeking coroutine.
            }

            return targetCreature != null; // Return true if a valid target creature was found, which means targetCreature is not null. If no valid target was found, targetCreature will still be null and we will return false.
        }

        private RagdollPart GetAimPart(Item arrow, Creature creature)
        {
            switch (_ModOptions.ragdollAimPart)
            {
                case "Closest Part":
                    return GetClosestPart(arrow, creature);
                case "Random":
                    var types = _ModOptions.AimPartDefinitions.Values.Select(d => d.PartType).ToList();
                    return creature.ragdoll.GetPart(types[Random.Range(0, types.Count)]);
                default:
                    if (_ModOptions.AimPartDefinitions.TryGetValue(_ModOptions.ragdollAimPart, out _ModOptions.AimPartDefinition def))
                        return creature.ragdoll.GetPart(def.PartType);
                    return null;
            }
        }

        private static RagdollPart GetClosestPart(Item arrow, Creature creature) // This function returns the closest RagdollPart of the creature to the arrow based on the angle between the arrow's forward direction and the direction to the part.
        {
            RagdollPart closest = null;
            float closestAngle = Mathf.Infinity;

            foreach (RagdollPart part in creature.ragdoll.parts)
            {
                float partAngle = Vector3.Angle(
                    part.transform.position - arrow.transform.position, // direction vector from the arrow to the part
                    arrow.flyDirRef.forward); // flyDirRef is the transform that represents the direction the arrow is flying towards.

                if (partAngle < closestAngle)
                {
                    closestAngle = partAngle;
                    closest = part;
                }
            }

            return closest;
        }

        private static Vector3 GetTargetPosition(Transform partTransform, Creature creature)
        {
            if (_ModOptions.AimPartDefinitions.TryGetValue(_ModOptions.ragdollAimPart, out _ModOptions.AimPartDefinition def))
                return def.GetPosition(partTransform, creature);

            return partTransform.position;
        }

        private static bool HasLineOfSight(Item arrow, Vector3 targetPos) // This function checks if there is a clear line of sight from the arrow to the target position. It casts a ray from the arrow's position towards the target position and checks if it hits a creature.
        {
            Vector3 dir = (targetPos - arrow.flyDirRef.transform.position).normalized; // flyDirRef "Used to point in direction when thrown.\nZ-Axis/Blue Arrow points forwards." So in plain english this is all saying: Get the direction vector from the arrow to the target position, and normalize it to get just the direction without affecting the magnitude. This will be used for the raycast to check line of sight.

            if (Physics.Raycast(arrow.flyDirRef.transform.position, dir, out RaycastHit hit, Mathf.Infinity, ~LayerMask.GetMask("TouchObject", "Zone", "LightProbeVolume", "PlayerHandAndFoot"))) // Reminder that flyDirRef is just where the arrow is pointing.  // Create a layer mask that excludes certain layers that we don't want to consider for line of sight, such as the player's own hands and feet, zones, light probe volumes, and touch objects. The ~ operator inverts the mask so that we are actually including all layers except these excluded ones.
                return hit.collider.transform.root.GetComponent<Creature>() != null; // If the raycast hits something, check if the root of the hit collider has a Creature component. If it does, that means we have a clear line of sight to the target position, since we hit a creature and not a wall or other obstacle. We return true in this case. If we hit something that doesn't have a Creature component, that means there is an obstacle in the way and we return false.

            return false;
        }


        public static void Save() // This function saves the list of excluded arrows to the save file. It creates an ExcludedArrow object with the list of excluded arrow IDs, serializes it to JSON, and writes it to the save file using the platform's WriteSaveCoroutine.
        {
            _ModOptions.arrows = new ExcludedArrow { excludedArrowsIds = _ModOptions.excludedArrows };
            GameManager.local.StartCoroutine(GameManager.platform.WriteSaveCoroutine(
                new PlatformBase.Save("ExcludedArrows", "sav",
                    JsonConvert.SerializeObject(_ModOptions.arrows, Catalog.GetJsonNetSerializerSettings()))));
        }

        public static IEnumerator Load() // This function loads the list of excluded arrows from the save file. It reads the save file using the platform's ReadSaveCoroutine, deserializes the JSON data into an ExcludedArrow object, and updates the mod options with the loaded list of excluded arrow IDs. If no save data is found, it initializes a new ExcludedArrow object and saves it to create the save file.
        {
            yield return GameManager.local.StartCoroutine(
                GameManager.platform.ReadSaveCoroutine("ExcludedArrows", "sav", save =>
                {
                    if (save != null)
                    {
                        _ModOptions.arrows = JsonConvert.DeserializeObject<ExcludedArrow>(save.data);
                        _ModOptions.excludedArrows = _ModOptions.arrows.excludedArrowsIds;
                    }
                    else
                    {
                        _ModOptions.arrows = new ExcludedArrow();
                        _ModOptions.excludedArrows = _ModOptions.arrows.excludedArrowsIds;
                        Save();
                    }
                }));
        }
    }
}