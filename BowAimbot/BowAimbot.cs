using System.Collections;
using System.Linq;
using ThunderRoad;
using UnityEngine;

namespace BasAimbot
{
    public class BowAimbot : ThunderScript
    {
        private const float ArrowSpeed = 30f;
        private const float ArrivalDistSqr = 0.15f;

        private Creature _targetCreature;
        private float _targetDistance = Mathf.Infinity;
        private Coroutine _seekCoroutine;

        public override void ScriptLoaded(ModManager.ModData modData)
        {
            base.ScriptLoaded(modData);
            EventManager.OnBowFireEvent += OnBowFire;
        }

        private void OnBowFire(RagdollHand hand, BowString bowString, Item arrow)
        {
            if (!_ModOptions.bowEnabled) return;
            if (arrow.lastHandler.creature != Player.currentCreature) return;

            _seekCoroutine = GameManager.local.StartCoroutine(SeekRoutine(arrow));
            arrow.OnFlyEndEvent += OnArrowFlyEnd;
            arrow.mainCollisionHandler.OnCollisionStartEvent += OnArrowCollision;
            foreach (Damager damager in arrow.mainCollisionHandler.damagers)
                damager.OnPenetrateEvent += OnArrowPenetrate;
        }

        private void CleanupArrow(Item arrow)
        {
            StopSeeking(arrow);
            arrow.OnFlyEndEvent -= OnArrowFlyEnd;
            arrow.mainCollisionHandler.OnCollisionStartEvent -= OnArrowCollision;
            foreach (Damager damager in arrow.mainCollisionHandler.damagers)
                damager.OnPenetrateEvent -= OnArrowPenetrate;
        }

        private void OnArrowPenetrate(Damager damager, CollisionInstance collision, EventTime eventTime)
        {
            if (eventTime == EventTime.OnStart) return;
            CleanupArrow(damager.collisionHandler.item);
        }

        private void OnArrowFlyEnd(Item arrow)
        {
            CleanupArrow(arrow);
        }

        private void OnArrowCollision(CollisionInstance collision)
        {
            if (collision.damageStruct.hitRagdollPart != null) return;
            CleanupArrow(collision.sourceColliderGroup.collisionHandler.item);
        }

        private void StopSeeking(Item arrow)
        {
            _targetCreature = null;
            _targetDistance = Mathf.Infinity;

            if (arrow?.physicBody?.rigidBody != null)
                arrow.physicBody.rigidBody.detectCollisions = true;

            if (_seekCoroutine != null)
            {
                GameManager.local.StopCoroutine(_seekCoroutine);
                _seekCoroutine = null;
            }
        }

        private IEnumerator SeekRoutine(Item arrow)
        {
            if (!TryFindTarget(arrow, out Transform targetTransform))
                yield break;

            while (arrow != null
                && _targetCreature != null
                && !_targetCreature.isKilled
                && _targetDistance <= _ModOptions.maxDistanceToCreature)
            {
                _targetDistance = Vector3.Distance(
                    _targetCreature.ragdoll.rootPart.transform.position,
                    arrow.transform.position);

                Vector3 targetPos = GetTargetPosition(targetTransform, _targetCreature);
                Vector3 toTarget = targetPos - arrow.transform.position;

                if (toTarget.sqrMagnitude > ArrivalDistSqr)
                {
                    Vector3 dir = toTarget.normalized;

                    if (_ModOptions.bowWallBang)
                    {
                        arrow.physicBody.rigidBody.detectCollisions = false;
                        arrow.physicBody.velocity = dir * ArrowSpeed * _ModOptions.bowSeekingSpeed;
                    }
                    else if (HasLineOfSight(arrow, targetPos, _targetDistance))
                    {
                        arrow.physicBody.velocity = dir * ArrowSpeed * _ModOptions.bowSeekingSpeed;
                    }
                }
                else
                {
                    StopSeeking(arrow);
                    yield break;
                }

                yield return null;
            }

            StopSeeking(arrow);
        }

        private bool TryFindTarget(Item arrow, out Transform targetTransform)
        {
            targetTransform = null;
            float closestAngle = Mathf.Infinity;
            _targetCreature = null;

            foreach (Creature creature in Creature.allActive)
            {
                if (creature == null || creature.isKilled || creature.isPlayer) continue;

                RagdollPart aimPart = GetAimPart(arrow, creature);
                if (aimPart == null) continue;

                float angleToCreature = Vector3.Angle(
                    aimPart.transform.position - arrow.transform.position,
                    arrow.flyDirRef.forward);

                if (angleToCreature >= closestAngle || angleToCreature > _ModOptions.angle) continue;

                closestAngle = angleToCreature;
                _targetCreature = creature;
                _targetDistance = Vector3.Distance(
                    creature.ragdoll.rootPart.transform.position,
                    arrow.transform.position);
                targetTransform = aimPart.transform;
            }

            return _targetCreature != null;
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

        private static RagdollPart GetClosestPart(Item arrow, Creature creature)
        {
            RagdollPart closest = null;
            float closestAngle = Mathf.Infinity;

            foreach (RagdollPart part in creature.ragdoll.parts)
            {
                float partAngle = Vector3.Angle(
                    part.transform.position - arrow.transform.position,
                    arrow.flyDirRef.forward);

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

        private static bool HasLineOfSight(Item arrow, Vector3 targetPos, float maxDist)
        {
            Vector3 dir = (targetPos - arrow.flyDirRef.transform.position).normalized;

            if (Physics.Raycast(arrow.flyDirRef.transform.position, dir, out RaycastHit hit, maxDist,
                ~LayerMask.GetMask("TouchObject", "Zone", "LightProbeVolume", "PlayerHandAndFoot")))
                return hit.collider.transform.root.GetComponent<Creature>() != null;

            return false;
        }
    }
}
