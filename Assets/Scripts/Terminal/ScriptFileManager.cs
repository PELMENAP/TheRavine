using TMPro;
using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public static class ScriptFileManager
    {
        private const string FILES_LIST_KEY = "script_files_list";
        private const string FILE_CONTENT_PREFIX = "script_file_";

        public static void SaveFile(string fileName, string content)
        {
            SaveLoad.SaveEncryptedDataWithoutMarking($"{FILE_CONTENT_PREFIX}{fileName}", content);
            
            var filesList = GetFilesList();
            if (!filesList.Contains(fileName))
            {
                filesList.Add(fileName);
                SaveLoad.SaveEncryptedDataWithoutMarking(FILES_LIST_KEY, filesList);
            }
        }

        public static string LoadFile(string fileName)
        {
            try
            {
                return SaveLoad.LoadEncryptedDataWithoutMarking<string>($"{FILE_CONTENT_PREFIX}{fileName}");
            }
            catch
            {
                return null;
            }
        }

        public static List<string> GetFilesList()
        {
            try
            {
                return SaveLoad.LoadEncryptedDataWithoutMarking<List<string>>(FILES_LIST_KEY) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static void DeleteFile(string fileName)
        {
            SaveLoad.DeleteFile($"{FILE_CONTENT_PREFIX}{fileName}");
            
            var filesList = GetFilesList();
            filesList.Remove(fileName);
            SaveLoad.SaveEncryptedDataWithoutMarking(FILES_LIST_KEY, filesList);
        }

        public static bool FileExists(string fileName)
        {
            return SaveLoad.FileExists($"{FILE_CONTENT_PREFIX}{fileName}");
        }
    }
}