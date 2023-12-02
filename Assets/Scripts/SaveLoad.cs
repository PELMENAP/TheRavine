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

    // Сохранение данных
    public static void SaveData(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
    }

    // Получение данных
    public static string LoadData(string key)
    {
        return PlayerPrefs.GetString(key, ""); // Возвращает значение по ключу, если ключ не найден - возвращает пустую строку
    }
    // Сохранение настроек с шифрованием
    public static void SaveEncryptedSettings(string volume, string fullscreen)
    {
        string encryptedVolume = EncryptionUtility.Encrypt(volume, "yourEncryptionKey");
        string encryptedFullscreen = EncryptionUtility.Encrypt(fullscreen, "yourEncryptionKey");

        PlayerPrefs.SetString("EncryptedVolume", encryptedVolume);
        PlayerPrefs.SetString("EncryptedFullscreen", encryptedFullscreen);
        PlayerPrefs.Save();
    }

    // Получение настроек с расшифровкой
    public static string LoadDecryptedVolume()
    {
        string encryptedVolume = PlayerPrefs.GetString("EncryptedVolume", "");
        return EncryptionUtility.Decrypt(encryptedVolume, "yourEncryptionKey");
    }

    public static string LoadDecryptedFullscreen()
    {
        string encryptedFullscreen = PlayerPrefs.GetString("EncryptedFullscreen", "");
        return EncryptionUtility.Decrypt(encryptedFullscreen, "yourEncryptionKey");
    }


}