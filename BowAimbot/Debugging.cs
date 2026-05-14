//using UnityEngine; 
//using ThunderRoad;
//using System.Collections;

//namespace BasAimbot
//{
//    public class Debugging : ThunderScript
//    {
//        private RagdollHand hand;
//        public override void ScriptLoaded(ModManager.ModData modData)
//        {
//            base.ScriptLoaded(modData);
//            EventManager.OnToggleOptionsMenu += OnToggleOptionsMenu; 
//        }

//        private void OnToggleOptionsMenu(bool isVisible)
//        {
//            if (isVisible)
//            {
//                GameManager.local.StartCoroutine(LogItems()); 
//            }
//        }

//        private IEnumerator LogItems()
//        {
//            int frameCount = 0; 

//            while (frameCount < 10)
//            {
//                foreach (Item item in Item.allActive)
//                {
//                    Debug.Log($"Frame {frameCount} - Item: {item.data.id}");
//                }

//                frameCount++;

//                yield return null; 
//            }
//        }
//    }
//}