using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

public static class SaveLoad
{
    public static void Save(object obj)
    {
        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Create(Application.persistentDataPath + "/savedGames.gd");
        bf.Serialize(file, obj);
        file.Close();
    }

    public static T Load<T>(ref T obj)
    {
        if (File.Exists(Application.persistentDataPath + "/savedGames.gd"))
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(Application.persistentDataPath + "/savedGames.gd", FileMode.Open);
            obj = (T)bf.Deserialize(file);
            file.Close();
            return obj;
        }
        return default(T);
    }
}