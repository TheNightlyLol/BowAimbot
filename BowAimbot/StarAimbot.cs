using UnityEngine;
using ThunderRoad;
using System.Collections;
using System.Linq;

namespace BowAimbot
{
    public class StarAimbot : ThunderScript
    {
        private const float StarSpeed = 30f;
        private const float ArrivalDistSqr = 0.15f;
        private Creature targetCreature;
        private float targetDistance = Mathf.Infinity;
        private string ThrowingStar = "ThrowablesRaktaThrowingStar";
        
        private void Start()
        {
            EventManager.OnItemRelease += OnItemRelease;
        }

        private void OnItemRelease(Handle handle, RagdollHand hand, bool throwing)
        {

        }
    }
}
