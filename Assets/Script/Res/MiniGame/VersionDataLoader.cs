using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VersionDataLoader
{
    public VersionDataLoader(string versionStr) {
        string[] lines = versionStr.Split('\n');
        // 读取FileList文件名
        string line = lines[1].Trim();
        string[] datas = line.Split('=');
        FileListFileName = datas[1].Trim() + ".txt";
        // 读取资源版本
        line = lines[0].Trim();
        datas = line.Split('=');
        Version = datas[1].Trim();
    }

    public string FileListFileName {
        get;
        set;
    }

    public string Version {
        get;
        set;
    }
}
