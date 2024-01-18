using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

using TheRavine.Security;

namespace TheRavine.Base
{
    public class DataStorage : MonoBehaviour
    {
        public static bool loadkey = false;
        public static bool normkey = false;
        public static bool sceneClose;

        [SerializeField] public Data data;

        private void Awake()
        {
            sceneClose = false;
            if (loadkey)
            {
                data = SaveLoad.LoadEncryptedData<Data>(nameof(Data));
                // PlayerData.data.entityTrans.position = PlayerData.data.cachedCamera.transform.position = new Vector3(data.PlPos.x, data.PlPos.y, 0);
                // PlayerData.data.cachedCamera.transform.position += new Vector3(0, 0, -1);
                // MapGeneratorOld.seed = data.seed;
            }
            else
            {
                // MapGeneratorOld.seed = Random.Range(1, 10000);
            }
        }

        public void Save()
        {
            // data.PlPos = new Data.Vec3(PlayerData.data.entityTrans.position);
            // data.seed = MapGeneratorOld.seed;
            SaveLoad.SaveEncryptedData<Data>(nameof(Data), data);
        }

        public void QuittoMenu()
        {
            loadkey = false;
            normkey = false;
            sceneClose = true;
            DayCycle.closeThread = false;
            StartCoroutine(CloseScene());
        }

        private IEnumerator CloseScene()
        {
            yield return new WaitForSeconds(3f);
            SceneManager.LoadScene(0);
        }

        [System.Serializable]
        public struct Data
        {
            public int seed;
            public Vec3 PlPos;

            [System.Serializable]
            public struct Vec3
            {
                public float x, y, z;
                public Vec3(Vector3 position)
                {
                    this.x = position.x;
                    this.y = position.y;
                    this.z = position.z;
                }
            }
        }
    }
}