using UnityEngine;
using ThunderRoad;
using System.Collections;
using System.Linq;
using System.Security.AccessControl;
using ThunderRoad.AI.Get;
using System.Diagnostics;

namespace BasAimbot
{
    public class StarAimbot : ThunderScript
    {
        private const float StarSpeed = 30f;
        private const float ArrivalDistSqr = 0.15f;
        private Creature targetCreature;
        private float targetDistance = Mathf.Infinity;

        public override void ScriptLoaded(ModManager.ModData modData)
        {
            base.ScriptLoaded(modData);
            EventManager.OnItemRelease += OnItemRelease;
        }

        private void OnItemRelease(Handle handle, RagdollHand hand, bool throwing)
        {
            if (hand.creature != Player.currentCreature) return; 
            if (handle.item?.data?.id != "ThrowablesRaktaThrowingStar") return;
            if (!throwing) return; // So then simply dropping the item won't make it seek. 

            GameManager.local.StartCoroutine(SeekRoutine(handle.item));
            IgnoreAllStarCollisions();
            handle.item.OnFlyEndEvent += OnStarFlyEnd; 
            handle.item.mainCollisionHandler.OnCollisionStartEvent += OnStarCollision;

            foreach (Damager damager in handle.item.mainCollisionHandler.damagers)
                damager.OnPenetrateEvent += OnStarPenetrate;
        }

        private void IgnoreAllStarCollisions()
        {
            var stars = Item.allActive.Where(i => i.data.id == "ThrowablesRaktaThrowingStar").ToList();

            for (int i = 0; i < stars.Count; i++)
                for (int j = i + 1; j < stars.Count; j++)
                    foreach (ColliderGroup cg1 in stars[i].colliderGroups)
                        foreach (Collider c1 in cg1.colliders)
                            foreach (ColliderGroup cg2 in stars[j].colliderGroups)
                                foreach (Collider c2 in cg2.colliders)
                                    Physics.IgnoreCollision(c1, c2, true);
        }

        private void OnStarPenetrate(Damager damager, CollisionInstance collision, EventTime eventTime)
        {
            if (eventTime == EventTime.OnStart) return;
            Item star = damager.collisionHandler.item;
            Creature hitCreature = collision.damageStruct.hitRagdollPart?.ragdoll.creature;
            
            StopSeeking(star);
            star.OnFlyEndEvent -= OnStarFlyEnd;
            star.mainCollisionHandler.OnCollisionStartEvent -= OnStarCollision;
            damager.OnPenetrateEvent -= OnStarPenetrate;
        }   

        private void OnStarFlyEnd(Item star)
        {
            StopSeeking(star);
            star.OnFlyEndEvent -= OnStarFlyEnd;
            star.mainCollisionHandler.OnCollisionStartEvent -= OnStarCollision;
            foreach (Damager damager in star.mainCollisionHandler.damagers)
                damager.OnPenetrateEvent -= OnStarPenetrate;
        }

        private void OnStarCollision(CollisionInstance collision)
        {
            if (collision.damageStruct.hitRagdollPart != null) return;

            Item star = collision.sourceColliderGroup.collisionHandler.item;
            StopSeeking(star);
            star.OnFlyEndEvent -= OnStarFlyEnd;
            star.mainCollisionHandler.OnCollisionStartEvent -= OnStarCollision;

            foreach (Damager damager in star.mainCollisionHandler.damagers)
                damager.OnPenetrateEvent -= OnStarPenetrate;
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
                targetCreature = creature;
                targetDistance = Vector3.Distance(
                    creature.ragdoll.rootPart.transform.position,
                    star.transform.position);
                targetTransform = aimPart.transform;
            }

            return targetCreature != null; 
        }

        private IEnumerator SeekRoutine(Item star)
        {
            if (!TryFindTarget(star, out Transform targetTransform))
                yield break;

            while (star != null && targetCreature != null
                && !targetCreature.isKilled
                && targetDistance <= _ModOptions.maxDistanceToCreature)
            {
                targetDistance = Vector3.Distance(
                    targetCreature.ragdoll.rootPart.transform.position,
                    targetCreature.transform.position);

                Vector3 targetPos = GetTargetPosition(targetTransform, targetCreature);
                Vector3 toTarget = targetPos - star.transform.position;

                if (toTarget.sqrMagnitude > ArrivalDistSqr)
                {
                    Vector3 dir = toTarget.normalized;

                    if (_ModOptions.starWallBang)
                    {
                        star.physicBody.rigidBody.detectCollisions = false;
                        star.physicBody.rigidBody.velocity = dir * StarSpeed * _ModOptions.starSeekingSpeed;
                    }
                    else if (HasLineOfSight(star, targetPos))
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
            targetCreature = null;
            targetDistance = Mathf.Infinity;
            star.physicBody.rigidBody.detectCollisions = true;
            GameManager.local.StopCoroutine(SeekRoutine(star));
        }

        private static bool HasLineOfSight(Item star, Vector3 targetPos)
        {
            Vector3 dir = (targetPos - star.flyDirRef.transform.position).normalized;

            // Exclude non-solid layers that shouldn't block line of sight
            if (Physics.Raycast(star.flyDirRef.transform.position, dir, out RaycastHit hit, Mathf.Infinity,
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
    }
}
