using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class OpenDetectedGameFolder : MonoBehaviour
{
    public MonoBehaviour targetScript;
    public bool openFirstFoundOnly = true;

    public void OpenDetectedPath()
    {
        if (targetScript == null)
        {
            Debug.LogWarning("OpenDetectedGameFolder: targetScript is null.");
            return;
        }

        Type t = targetScript.GetType();
        MethodInfo[] methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        bool foundAny = false;

        foreach (var method in methods)
        {
            string name = method.Name;
            if (!name.StartsWith("Get", StringComparison.OrdinalIgnoreCase)) continue;
            if (!name.EndsWith("Path", StringComparison.OrdinalIgnoreCase)) continue;
            if (method.ReturnType != typeof(string)) continue;

            try
            {
                object instance = method.IsStatic ? null : (object)targetScript;

                var parameters = method.GetParameters();

                if (parameters.Length == 0)
                {
                    object result = method.Invoke(instance, null);
                    string path = result as string;
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    {
                        foundAny = true;
                        OpenFolder(path);
                        if (openFirstFoundOnly) return;
                    }
                }
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
                {
                    // try both true and false to cover methods like GetDS2Path(bool isScholar)
                    foreach (bool arg in new[] { true, false })
                    {
                        object result = method.Invoke(instance, new object[] { arg });
                        string path = result as string;
                        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                        {
                            foundAny = true;
                            OpenFolder(path);
                            if (openFirstFoundOnly) return;
                        }
                    }
                }
                else
                {
                    // skip other signatures
                    continue;
                }
            }
            catch { }
        }

        if (!foundAny)
            Debug.LogWarning("OpenDetectedGameFolder: no valid folder found by Get...Path methods.");
    }

    void OpenFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            Debug.LogWarning("OpenDetectedGameFolder: invalid or not found path: " + path);
            return;
        }

        bool opened = false;

        try
        {
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            {
                try
                {
                    var psi = new ProcessStartInfo("explorer.exe")
                    {
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    opened = true;
                }
                catch { }

                if (!opened)
                {
                    try
                    {
                        var psi2 = new ProcessStartInfo()
                        {
                            FileName = path,
                            UseShellExecute = true
                        };
                        Process.Start(psi2);
                        opened = true;
                    }
                    catch { }
                }

                if (!opened)
                {
                    try
                    {
                        string fileUrl = "file:///" + path.Replace('\\', '/');
                        Application.OpenURL(fileUrl);
                        opened = true;
                    }
                    catch { }
                }
            }
            else if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
            {
                try
                {
                    var psi = new ProcessStartInfo("open")
                    {
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    opened = true;
                }
                catch { }

                if (!opened)
                {
                    try
                    {
                        string fileUrl = "file://" + path;
                        Application.OpenURL(fileUrl);
                        opened = true;
                    }
                    catch { }
                }
            }
        }
        catch { }

        if (!opened)
            Debug.LogWarning("OpenDetectedGameFolder: could not open folder: " + path);
    }
}