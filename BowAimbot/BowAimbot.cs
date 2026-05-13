using System.Collections;
using System.Linq;
using ThunderRoad;
using UnityEngine;

namespace BowAimbot
{
    public class BowAimbot : ThunderScript
    {
        private const float ArrowSpeed = 30f;
        private const float ArrivalDistSqr = 0.15f; 
        private Creature targetCreature;
        private float targetDistance = Mathf.Infinity;

        public override void ScriptLoaded(ModManager.ModData modData)
        {
            base.ScriptLoaded(modData);
            EventManager.OnBowFireEvent += OnBowFire;
        }

        private void OnBowFire(RagdollHand hand, BowString bowString, Item arrow)
        {
            if (!_ModOptions.enabled) return;
            if (arrow.lastHandler.creature != Player.currentCreature) return;

            GameManager.local.StartCoroutine(SeekRoutine(arrow));
            arrow.OnFlyEndEvent += OnArrowFlyEnd;
            arrow.mainCollisionHandler.OnCollisionStartEvent += OnArrowCollision;

            // for each damager on the arrow, subscribe to the penetrate event so we can stop seeking if the arrow penetrates something.
            foreach (Damager damager in arrow.mainCollisionHandler.damagers)
                damager.OnPenetrateEvent += OnArrowPenetrate; 
        }

        private void OnArrowFlyEnd(Item arrow)
        {
            StopSeeking(arrow);
            arrow.OnFlyEndEvent -= OnArrowFlyEnd;
            arrow.mainCollisionHandler.OnCollisionStartEvent -= OnArrowCollision;

            // For each damager on the arrow, unsubscribe from the penetrate event to prevent memory leaks.
            foreach (Damager damager in arrow.mainCollisionHandler.damagers)
                damager.OnPenetrateEvent -= OnArrowPenetrate;
        }

        private void OnArrowCollision(CollisionInstance collision)
        {
            Item arrow = collision.sourceColliderGroup.collisionHandler.item;
            StopSeeking(arrow);
            arrow.OnFlyEndEvent -= OnArrowFlyEnd;
            arrow.mainCollisionHandler.OnCollisionStartEvent -= OnArrowCollision;
            foreach (Damager damager in arrow.mainCollisionHandler.damagers)
                damager.OnPenetrateEvent -= OnArrowPenetrate;
        }

        private void OnArrowPenetrate(Damager damager, CollisionInstance collision, EventTime eventTime)
        {
            if (eventTime == EventTime.OnStart) return;

            Item arrow = damager.collisionHandler.item;
            StopSeeking(arrow);

            if (_ModOptions.bigBoomBoom)
            {
                if (Physics.Raycast(arrow.transform.position, arrow.flyDirRef.forward, out RaycastHit hit, 200f,
                    ~LayerMask.GetMask("TouchObject", "Zone", "LightProbeVolume", "PlayerHandAndFoot")))
                {
                    Creature hitCreature = hit.collider?.transform.root.GetComponent<Creature>();

                    if (hitCreature != null && !hitCreature.isKilled)
                    {
                        EffectData effectData = Catalog.GetData<EffectData>("SpellGravityPush", true);
                        if (effectData != null)
                        {
                            EffectInstance effect = effectData.Spawn(hit.point, Quaternion.LookRotation(hit.normal));
                            effect.Play();
                            hitCreature.AddForce((hitCreature.transform.position - hit.point).normalized * 20f, ForceMode.Impulse);
                            GameManager.local.StartCoroutine(DespawnAfter(effect, 3f));
                        }
                    }
                }
            }

            arrow.OnFlyEndEvent -= OnArrowFlyEnd;
            arrow.mainCollisionHandler.OnCollisionStartEvent -= OnArrowCollision;
            damager.OnPenetrateEvent -= OnArrowPenetrate;
        }

        private IEnumerator DespawnAfter(EffectInstance effect, float delay)
        {
            yield return new WaitForSeconds(delay);
            effect?.Despawn();
        }

        private void StopSeeking(Item arrow)
        {
            targetCreature = null;
            targetDistance = Mathf.Infinity;
            arrow.physicBody.rigidBody.detectCollisions = true;
            GameManager.local.StopCoroutine(SeekRoutine(arrow));
        }

        // ── Seeking Coroutine ─────────────────────────────────────────────────────

        private IEnumerator SeekRoutine(Item arrow)
        {
            if (!TryFindTarget(arrow, out Transform targetTransform))
                yield break;

            while (arrow != null && targetCreature != null
                && !targetCreature.isKilled
                && targetDistance <= _ModOptions.maxDistanceToCreature)
            {
                targetDistance = Vector3.Distance(
                    targetCreature.ragdoll.rootPart.transform.position,
                    arrow.transform.position);

                Vector3 targetPos = GetTargetPosition(targetTransform, targetCreature);
                Vector3 toTarget = targetPos - arrow.transform.position;

                if (toTarget.sqrMagnitude > ArrivalDistSqr)
                {
                    Vector3 dir = toTarget.normalized;

                    if (_ModOptions.wallBang)
                    {
                        // Disable collisions so the arrow can pass through walls
                        arrow.physicBody.rigidBody.detectCollisions = false;
                        arrow.physicBody.velocity = dir * ArrowSpeed * _ModOptions.seekingSpeed;
                    }
                    else if (HasLineOfSight(arrow, targetPos))
                    {
                        arrow.physicBody.velocity = dir * ArrowSpeed * _ModOptions.seekingSpeed;
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
                targetCreature = creature;
                targetDistance = Vector3.Distance(
                    creature.ragdoll.rootPart.transform.position,
                    arrow.transform.position);
                targetTransform = aimPart.transform;
            }

            return targetCreature != null;
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

        // Returns the ragdoll part closest to the arrow's current flight direction
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

        private static bool HasLineOfSight(Item arrow, Vector3 targetPos)
        {
            Vector3 dir = (targetPos - arrow.flyDirRef.transform.position).normalized;

            // Exclude non-solid layers that shouldn't block line of sight
            if (Physics.Raycast(arrow.flyDirRef.transform.position, dir, out RaycastHit hit, Mathf.Infinity,
                ~LayerMask.GetMask("TouchObject", "Zone", "LightProbeVolume", "PlayerHandAndFoot")))
                return hit.collider.transform.root.GetComponent<Creature>() != null;

            return false;
        }
    }
}