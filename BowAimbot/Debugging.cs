using UnityEngine; 
using ThunderRoad;

namespace BasAimbot
{
    public class Debugging : ThunderScript
    {
        private RagdollHand hand;

        public override void ScriptLoaded(ModManager.ModData modData)
        {
            base.ScriptLoaded(modData);
            EventManager.onItemEnterTrigger += OnItemEnterTrigger;
        }

        private void OnItemEnterTrigger(Item item, string triggerTag)
        {
            if (item?.data?.id != "CrystalMusket") return;
            if (hand.creature != Player.currentCreature) return; 
            foreach (Item i in Item.allActive)
            {
                Debug.Log($"Item ID's: {i.data.id} and this is the triggerTag to see what it is: {triggerTag}");
            }
        }
    }
}