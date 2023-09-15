using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class DataStorage : MonoBehaviour
{
    public static bool loadkey = false;
    public static bool normkey = false;

    [SerializeField] public Data data;

    private void Awake()
    {
        if (loadkey)
        {
            SaveLoad.Load(ref data);
            PlayerController.entityTrans.position = PlayerController.instance.cachedCamera.transform.position = new Vector3(data.playerPosition.x, data.playerPosition.y, 0);
            PlayerController.instance.cachedCamera.transform.position += new Vector3(0, 0, -1);
            MapGenerator.seed = data.seed;
        }
        else
        {
            MapGenerator.seed = Random.Range(1, 10000);
        }
    }

    public void Save()
    {
        data.playerPosition = new Data.Vec3(PlayerController.entityTrans.position);
        data.seed = MapGenerator.seed;
        SaveLoad.Save(data);
    }

    public void QuittoMenu()
    {
        loadkey = false;
        normkey = false;
        StartCoroutine(UpdateDay());
    }

    private IEnumerator UpdateDay()
    {
        DayCycle.closeThread = false;
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene(0);
    }

    [System.Serializable]
    public class Data
    {
        public int seed;
        public Vec3 playerPosition;

        [System.Serializable]
        public struct Vec3
        {
            public float x, y, z;
            public Vec3(float x, float y, float z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
            public Vec3(Vector3 position)
            {
                this.x = position.x;
                this.y = position.y;
                this.z = position.z;
            }
        }
    }
}
