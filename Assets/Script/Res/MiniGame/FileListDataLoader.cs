using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FileListDataLoader
#if UNITY_WEIXINMINIGAME
    : IWXAssetBundleMapper
#endif
{
    public FileListDataLoader(string fileListStr) {
        string[] lines = fileListStr.Split('\n');
        LoadLines(lines);
    }

    protected void LoadLines(string[] lines) {
        m_FileListMap.Clear();
        foreach (var line in lines) {
            if (string.IsNullOrEmpty(line))
                continue;
            var data = line.Split('=');
            var key = data[0];
            var values = data[1].Split(';');
            var value = values[0];
            m_FileListMap[key] = value;
        }
    }

    public string GetCDNFileName(string oriFileName) {
        if (string.IsNullOrEmpty(oriFileName))
            return string.Empty;
        string ret;
        if (m_FileListMap.TryGetValue(oriFileName, out ret))
            return ret;
        return string.Empty;
    }

    private Dictionary<string, string> m_FileListMap = new Dictionary<string, string>();
}
