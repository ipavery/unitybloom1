using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Runtime.InteropServices;

public static class SaveSystem
{
    [DllImport("__Internal")]
    private static extern void SyncFiles();

    private static string IndexFile => Path.Combine(Application.persistentDataPath, "saves_index.json");

    /// <summary>
    /// Permanently delete all save files that match the "save_*.json" pattern
    /// and remove the index file.
    /// </summary>
    public static void ClearAllSaves()
    {
        try
        {
            var dir = Application.persistentDataPath;
            Debug.Log($"[SaveSystem] Clearing all saves in: {dir}");

            // Delete all files that look like save files (prefix used by SaveSceneAs)
            foreach (var f in Directory.GetFiles(dir, "save_*.json"))
            {
                try { File.Delete(f); Debug.Log($"[SaveSystem] Deleted: {f}"); }
                catch (System.Exception ex) { Debug.LogWarning($"[SaveSystem] Could not delete {f}: {ex.Message}"); }
            }

            // Also try to delete any stray files you may have used
            foreach (var f in Directory.GetFiles(dir, "*.json"))
            {
                // optional: skip other unrelated JSONs by being conservative.
                if (Path.GetFileName(f) == Path.GetFileName(IndexFile)) continue;
                // If you want to be strict about only deleting your saves, omit this loop.
            }

            // Remove index file
            if (File.Exists(IndexFile))
            {
                File.Delete(IndexFile);
                Debug.Log($"[SaveSystem] Deleted index file: {IndexFile}");
            }

            Debug.Log("[SaveSystem] All saves cleared.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[SaveSystem] Exception while clearing saves: " + ex);
        }
    }

    public static List<SaveMeta> LoadIndex()
    {
        if (!File.Exists(IndexFile)) return new List<SaveMeta>();
        string json = File.ReadAllText(IndexFile);
        // Wrap because JsonUtility can't serialize top-level lists directly
        var wrapper = JsonUtility.FromJson<SaveMetaListWrapper>(json);
        return wrapper?.list ?? new List<SaveMeta>();
    }

    public static void SaveIndex(List<SaveMeta> metas)
    {
        var wrapper = new SaveMetaListWrapper { list = metas };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(IndexFile, json);
    }

    public static string SaveSceneAs(SaveData data, string displayName)
    {
        string filename = $"save_{DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")}_{Guid.NewGuid()}.json";
        string path = Path.Combine(Application.persistentDataPath, filename);
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);

        var metas = LoadIndex();
        var meta = new SaveMeta
        {
            displayName = displayName,
            filename = filename,
            timestamp = DateTime.UtcNow.Ticks
        };
        metas.Insert(0, meta); // newest first
        SaveIndex(metas);

        #if UNITY_WEBGL && !UNITY_EDITOR
            SyncFiles();
        #endif

        return filename;
    }

    public static SaveData LoadSaveFile(string filename)
    {
        string path = Path.Combine(Application.persistentDataPath, filename);
        if (!File.Exists(path)) return null;
        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<SaveData>(json);
    }

    public static void DeleteSave(string filename)
    {
        string path = Path.Combine(Application.persistentDataPath, filename);
        if (File.Exists(path)) File.Delete(path);

        var metas = LoadIndex();
        metas.RemoveAll(m => m.filename == filename);
        SaveIndex(metas);

        #if UNITY_WEBGL && !UNITY_EDITOR
            SyncFiles();
        #endif
    }

    // Helper wrapper for serializing lists with JsonUtility
    [Serializable]
    private class SaveMetaListWrapper { public List<SaveMeta> list = new List<SaveMeta>(); }
}