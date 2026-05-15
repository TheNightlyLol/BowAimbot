using UnityEngine;
using ThunderRoad;
using System.Collections;
using System.Linq;


namespace BasAimbot
{
    public class GunAimbot : ThunderScript
    {
        private Creature _targetCreature;
        private Item _projectile;

        private const float ShardSpeed = 30f;
        private const float ArrivalDistSqr = 0.15f;
        private float _targetDistance = Mathf.Infinity;

        private bool _playerHoldingMusket = false;

        public override void ScriptLoaded(ModManager.ModData modData)
        {
            base.ScriptLoaded(modData);
            Item.OnItemSpawn += OnItemSpawn;
            EventManager.onLevelLoad += onLevelLoad;
            EventManager.OnItemGrab += OnItemGrab;
            EventManager.OnItemRelease += OnItemRelease;
        }

        private void OnItemGrab(Handle handle, RagdollHand hand)
        {
            if (handle.item?.data?.id != "CrystalMusket") return;
            if (hand.creature != Player.currentCreature) return;
            _playerHoldingMusket = true;
        }

        private void OnItemRelease(Handle handle, RagdollHand hand, bool throwing)
        {
            if (handle.item?.data?.id != "CrystalMusket") return;
            _playerHoldingMusket = false;
        }

        private void onLevelLoad(LevelData level, LevelData.Mode mode, EventTime eventTime)
        {
            if (Player.currentCreature != null) Debug.Log($"Player Loaded.");
            Debug.Log(_projectile);
        }

        private void OnItemSpawn(Item i)
        {
            if (i.data.id != "DynamicAreaProjectile" && i.data.id != "DynamicProjectile") return;
            if (!_playerHoldingMusket) return;
            if (!_ModOptions.ShardEnabled) return;

            _projectile = i;
            GameManager.local.StartCoroutine(SeekRoutine());
            _projectile.mainCollisionHandler.OnCollisionStartEvent += OnCollision;
        }

        private IEnumerator SeekRoutine()
        {
            if (!TryFindTarget(_projectile, out Transform targetTransform)) yield break;

            while (_projectile != null && _targetCreature != null && !_targetCreature.isKilled && _targetDistance <= _ModOptions.maxDistanceToCreature)
            {
                _targetDistance = Vector3.Distance(
                    _targetCreature.ragdoll.rootPart.transform.position,
                    _projectile.transform.position);


                Vector3 targetPos = GetTargetPosition(targetTransform, _targetCreature);
                Vector3 toTarget = targetPos - _projectile.transform.position;

                if (toTarget.sqrMagnitude > ArrivalDistSqr)
                {
                    Vector3 dir = toTarget.normalized;

                    if (_ModOptions.ShardWallBang)
                    {
                        _projectile.physicBody.rigidBody.detectCollisions = false;
                        _projectile.physicBody.rigidBody.velocity = dir * (ShardSpeed * _ModOptions.ShardSeekingSpeed);
                    }
                    else if (HasLineOfSight(targetPos))
                    {
                        _projectile.physicBody.velocity = dir * (ShardSpeed * _ModOptions.ShardSeekingSpeed);
                    }
                }
                else
                {
                    StopSeeking();
                    yield break;
                }

                yield return null;
            }

            StopSeeking();
        }

        private void OnCollision(CollisionInstance collision)
        {
            if (collision.damageStruct.hitRagdollPart != null) return;

            StopSeeking();
            _projectile.mainCollisionHandler.OnCollisionStartEvent -= OnCollision;
        }

        private void StopSeeking()
        {
            _projectile.mainCollisionHandler.OnCollisionStartEvent -= OnCollision;
            _targetCreature = null;
            _targetDistance = Mathf.Infinity;
            _projectile.physicBody.rigidBody.detectCollisions = true;
            GameManager.local.StopCoroutine(SeekRoutine());
        }

        private bool HasLineOfSight(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - _projectile.flyDirRef.transform.position).normalized;

            if (Physics.Raycast(_projectile.flyDirRef.transform.position, dir, out RaycastHit hit, _targetDistance,
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

        private bool TryFindTarget(Item star, out Transform targetTransform)
        {
            targetTransform = null;
            float closestAngle = Mathf.Infinity;

            foreach (Creature creature in Creature.allActive)
            {
                if (creature == null || creature.isKilled || creature.isPlayer) continue;

                RagdollPart aimPart = GetAimPart(creature);
                if (aimPart == null) continue;

                float angleToCreature = Vector3.Angle(
                    aimPart.transform.position - star.transform.position,
                    star.flyDirRef.forward);

                if (angleToCreature >= closestAngle || angleToCreature > _ModOptions.angle) continue;

                closestAngle = angleToCreature;
                _targetCreature = creature;
                _targetDistance = Vector3.Distance(
                    creature.ragdoll.rootPart.transform.position,
                    star.transform.position);
                targetTransform = aimPart.transform;
            }

            return _targetCreature != null;
        }

        private RagdollPart GetAimPart(Creature creature)
        {
            switch (_ModOptions.ragdollAimPart)
            {
                case "Closest Part":
                    return GetClosestPart(creature);
                case "Random":
                    var types = _ModOptions.AimPartDefinitions.Values.Select(d => d.PartType).ToList();
                    return creature.ragdoll.GetPart(types[Random.Range(0, types.Count)]);
                default:
                    if (_ModOptions.AimPartDefinitions.TryGetValue(_ModOptions.ragdollAimPart, out _ModOptions.AimPartDefinition def))
                        return creature.ragdoll.GetPart(def.PartType);
                    return null;
            }
        }

        private RagdollPart GetClosestPart(Creature creature)
        {
            RagdollPart closest = null;
            float closestAngle = Mathf.Infinity;

            foreach (RagdollPart part in creature.ragdoll.parts)
            {
                float partAngle = Vector3.Angle(
                    part.transform.position - _projectile.transform.position,
                    _projectile.flyDirRef.forward);

                if (partAngle < closestAngle)
                {
                    closestAngle = partAngle;
                    closest = part;
                }
            }

            return closest;
        }
    }
}
