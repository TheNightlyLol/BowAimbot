//using UnityEngine;
//using ThunderRoad;
//using System.Collections;
//using System.Linq;


//namespace BasAimbot
//{
//    public class GunAimbot : ThunderScript
//    {
//        private RagdollHand hand;
//        private Handle handle;
//        private const float FireSpeed = 30f;
//        private const float ArrivalDistSqr = 0.15f;
//        private Creature targetCreature;
//        private float targetDistance = Mathf.Infinity;
//        public override void ScriptLoaded(ModManager.ModData modData)
//        {
//            base.ScriptLoaded(modData);
//            EventManager.onItemEnterTrigger += OnItemEnterTrigger;
//        }

//        private void OnItemEnterTrigger(Item item, string triggerTag)
//        {
//            if (item?.data?.id != "CrystalMusket") return;
//            if (hand.creature != Player.currentCreature) return;

//            GameManager.local.StartCoroutine(SeekRoutine(item));
//            handle.item.mainCollisionHandler.OnCollisionStartEvent += OnCollision;
//        }

//        private IEnumerator SeekRoutine(Item item)
//        {
//            if (!TryFindTarget(item, out Transform targetTransform))
//                yield break;
//        }

//        private bool TryFindTarget(Item star, out Transform targetTransform)
//        {
//            targetTransform = null;
//            float closestAngle = Mathf.Infinity;

//            foreach (Creature creature in Creature.allActive)
//            {
//                if (creature == null || creature.isKilled || creature.isPlayer) continue;

//                RagdollPart aimPart = GetAimPart(star, creature);
//                if (aimPart == null) continue;

//                float angleToCreature = Vector3.Angle(
//                    aimPart.transform.position - star.transform.position,
//                    star.flyDirRef.forward);

//                if (angleToCreature >= closestAngle || angleToCreature > _ModOptions.angle) continue;

//                closestAngle = angleToCreature;
//                targetCreature = creature;
//                targetDistance = Vector3.Distance(
//                    creature.ragdoll.rootPart.transform.position,
//                    star.transform.position);
//                targetTransform = aimPart.transform;
//            }

//            return targetCreature != null;
//        }

//        private RagdollPart GetAimPart(Item star, Creature creature)
//        {
//            switch (_ModOptions.ragdollAimPart)
//            {
//                case "Closest Part":
//                    return GetClosestPart(star, creature);
//                case "Random":
//                    var types = _ModOptions.AimPartDefinitions.Values.Select(d => d.PartType).ToList();
//                    return creature.ragdoll.GetPart(types[Random.Range(0, types.Count)]);
//                default:
//                    if (_ModOptions.AimPartDefinitions.TryGetValue(_ModOptions.ragdollAimPart, out _ModOptions.AimPartDefinition def))
//                        return creature.ragdoll.GetPart(def.PartType);
//                    return null;
//            }
//        }

//        private static RagdollPart GetClosestPart(Item star, Creature creature)
//        {
//            RagdollPart closest = null;
//            float closestAngle = Mathf.Infinity;

//            foreach (RagdollPart part in creature.ragdoll.parts)
//            {
//                float partAngle = Vector3.Angle(
//                    part.transform.position - star.transform.position,
//                    star.flyDirRef.forward);

//                if (partAngle < closestAngle)
//                {
//                    closestAngle = partAngle;
//                    closest = part;
//                }
//            }

//            return closest;
//        }
//    }
//}
