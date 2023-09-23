using System.Collections.Generic;
using UnityEngine;

namespace Modern2D
{

    //  Holds the information about shadow
    //  Persistent through the editor so the 
    //  system don't have to create new shadows every time you click "play",
    //  or when the game starts (build)

    [System.Serializable]
    public class ShadowData 
    {
        /// <summary>
        /// inefficient, use in worst case scenarios, 
        ///  or if you have a small scene and a couple of shadows
        /// </summary>
        /// <returns></returns>
        public static Dictionary<Transform, StylizedShadowCaster> GetAllCastersInTheScene()
        {
            Dictionary<Transform, StylizedShadowCaster> casters = new Dictionary<Transform, StylizedShadowCaster>();
            foreach (var a in Transform.FindObjectsOfType<StylizedShadowCaster2D>())
            {
                LightingSystem.system.AddShadow(a.shadowData);
                casters.Add(a.transform, a.shadowData.shadow);
            }
            return casters;
        }

        /// <summary>
        /// can be used to check if the shadow wasn't edited
        /// </summary>
        public static StylizedShadowCaster defaultValue;

        /// <summary>
        /// Holds the whole shadow data
        /// </summary>
        public StylizedShadowCaster shadow;

        public bool HasShadow() => shadow.Equals(defaultValue);

        /// <summary>
        /// Can leave the pivot standing if shadows are often changed and you need preformance
        /// </summary>
        public void Delete(bool deletePivot = false)
        {
            if (deletePivot)
                GameObject.Destroy(shadow.shadowPivot);
            GameObject.Destroy(shadow.shadow.gameObject);
        }
    }

}