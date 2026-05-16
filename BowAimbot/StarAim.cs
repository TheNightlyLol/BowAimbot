using UnityEngine;
using ThunderRoad;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

namespace BasAimbot
{
    public class StarAim : ThunderScript
    {
        private const float StarSpeed = 30f;
        private const float ArrivalDistSqr = 0.15f;
        private readonly Dictionary<Item, Coroutine> _activeCoroutines = new Dictionary<Item, Coroutine>();
        private readonly Dictionary<Item, Creature> _targetCreatures = new Dictionary<Item, Creature>();
        private readonly Dictionary<Item, float> _targetDistances = new Dictionary<Item, float>();

        public override void ScriptLoaded(ModManager.ModData modData)
        {
            base.ScriptLoaded(modData);
            EventManager.OnItemRelease += OnItemRelease;
        }

        private void OnItemRelease(Handle handle, RagdollHand hand, bool throwing)
        {
            if (hand.creature != Player.currentCreature || handle.item?.data?.id != "ThrowablesRaktaThrowingStar") return;
            if (hand.physicBody.velocity.sqrMagnitude <= 9f) return;

            _activeCoroutines[handle.item] = GameManager.local.StartCoroutine(SeekRoutine(handle.item));
            IgnoreNewStarCollisions(handle.item);
            handle.item.OnFlyEndEvent += OnStarFlyEnd; 
            handle.item.mainCollisionHandler.OnCollisionStartEvent += OnStarCollision;

            foreach (Damager damager in handle.item.mainCollisionHandler.damagers)
                damager.OnPenetrateEvent += OnStarPenetrate;
        }

        private void CleanupStar(Item star)
        {
            StopSeeking(star);
            star.OnFlyEndEvent -= OnStarFlyEnd;
            star.mainCollisionHandler.OnCollisionStartEvent -= OnStarCollision;
            foreach (Damager damager in star.mainCollisionHandler.damagers)
                damager.OnPenetrateEvent -= OnStarPenetrate;
        }

        private void OnStarPenetrate(Damager damager, CollisionInstance collision, EventTime eventTime)
        {
            if (eventTime == EventTime.OnStart) return;
            Item star = damager.collisionHandler.item;
            CleanupStar(star);
        }   

        private void OnStarFlyEnd(Item star)
        {
            CleanupStar(star);
        }

        private void OnStarCollision(CollisionInstance collision)
        {
            if (collision.damageStruct.hitRagdollPart != null) return;

            Item star = collision.sourceColliderGroup.collisionHandler.item;
            CleanupStar(star);
        }

        private RagdollPart GetAimPart(Item star, Creature creature)
        {
            switch (_ModOptions.ragdollAimPart)
            {
                case "Closest Part":
                    return GetClosestPart(star, creature);
                case "Random":
                    var types = _ModOptions.AimPartDefinitions.Values.Select(d => d.PartType).ToList();
                    return creature.ragdoll.GetPart(types[Random.Range(0, types.Count)]);
                default:
                    if (_ModOptions.AimPartDefinitions.TryGetValue(_ModOptions.ragdollAimPart, out _ModOptions.AimPartDefinition def))
                        return creature.ragdoll.GetPart(def.PartType);
                    return null;
            }
        }

        private static RagdollPart GetClosestPart(Item star, Creature creature)
        {
            RagdollPart closest = null;
            float closestAngle = Mathf.Infinity;

            foreach (RagdollPart part in creature.ragdoll.parts)
            {
                float partAngle = Vector3.Angle(
                    part.transform.position - star.transform.position,
                    star.flyDirRef.forward);

                if (partAngle < closestAngle)
                {
                    closestAngle = partAngle;
                    closest = part;
                }
            }

            return closest;
        }

        private bool TryFindTarget(Item star, out Transform targetTransform)
        {
            targetTransform = null;
            float closestAngle = Mathf.Infinity;
            _targetCreatures[star] = null;

            foreach (Creature creature in Creature.allActive)
            {
                if (creature == null || creature.isKilled || creature.isPlayer) continue;

                RagdollPart aimPart = GetAimPart(star, creature);
                if (aimPart == null) continue;

                float angleToCreature = Vector3.Angle(
                    aimPart.transform.position - star.transform.position,
                    star.flyDirRef.forward);

                if (angleToCreature >= closestAngle || angleToCreature > _ModOptions.angle) continue;

                closestAngle = angleToCreature;
                _targetCreatures[star] = creature;
                _targetDistances[star] = Vector3.Distance(
                    creature.ragdoll.rootPart.transform.position,
                    star.transform.position);
                targetTransform = aimPart.transform;
            }

            return _targetCreatures.TryGetValue(star, out Creature t) && t != null;
        }

        private IEnumerator SeekRoutine(Item star)
        {
            if (!TryFindTarget(star, out Transform targetTransform)) yield break;

            while (star != null
                && _targetCreatures.TryGetValue(star, out Creature tc) && tc != null
                && !tc.isKilled
                && _targetDistances.TryGetValue(star, out float td) && td <= _ModOptions.maxDistanceToCreature)
            {
                _targetDistances[star] = Vector3.Distance(
                    tc.ragdoll.rootPart.transform.position,
                    star.transform.position);
                float currentDist = _targetDistances[star];

                Vector3 targetPos = GetTargetPosition(targetTransform, tc);
                Vector3 toTarget = targetPos - star.transform.position;

                if (toTarget.sqrMagnitude > ArrivalDistSqr)
                {
                    Vector3 dir = toTarget.normalized;
                    if (_ModOptions.starWallBang)
                    {
                        star.physicBody.rigidBody.detectCollisions = false;
                        star.physicBody.rigidBody.velocity = dir * StarSpeed * _ModOptions.starSeekingSpeed;
                    }
                    else if (HasLineOfSight(star, targetPos, currentDist))
                    {
                        star.physicBody.velocity = dir * StarSpeed * _ModOptions.starSeekingSpeed;
                    }
                }
                else
                {
                    StopSeeking(star);
                    yield break;
                }

                yield return null;
            }

            StopSeeking(star);
        }

        private void StopSeeking(Item star)
        {
            _targetCreatures.Remove(star);
            _targetDistances.Remove(star);
            star.physicBody.rigidBody.detectCollisions = true;

            if (_activeCoroutines.TryGetValue(star, out Coroutine c))
            {
                GameManager.local.StopCoroutine(c);
                _activeCoroutines.Remove(star);
            }
        }

        private static bool HasLineOfSight(Item star, Vector3 targetPos, float maxDist)
        {
            Vector3 dir = (targetPos - star.flyDirRef.transform.position).normalized;

            if (Physics.Raycast(star.flyDirRef.transform.position, dir, out RaycastHit hit, maxDist,
                ~LayerMask.GetMask("TouchObject", "Zone", "LightProbeVolume", "PlayerHandAndFoot")))
                return hit.collider.transform.root.GetComponent<Creature>() != null;

            return false;
        }

        private static Vector3 GetTargetPosition(Transform partTransform, Creature creature)
        {
            if (_ModOptions.AimPartDefinitions.TryGetValue(_ModOptions.ragdollAimPart, out _ModOptions.AimPartDefinition def))
                return def.GetPosition(partTransform, creature);

            return partTransform.position;
        }

        private void IgnoreNewStarCollisions(Item newStar)
        {
            foreach (Item star in Item.allActive.Where(i => i.data.id == "ThrowablesRaktaThrowingStar" && i != newStar))
                foreach (ColliderGroup cg1 in newStar.colliderGroups)
                    foreach (Collider c1 in cg1.colliders)
                        foreach (ColliderGroup cg2 in star.colliderGroups)
                            foreach (Collider c2 in cg2.colliders)
                                Physics.IgnoreCollision(c1, c2, true);
        }
    }
}