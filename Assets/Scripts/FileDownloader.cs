using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.IO.Compression;

public class FileDownloader : MonoBehaviour
{
    [System.Serializable]
    public class FileData
    {
        public string url;
        public string fileName;
    }

    // ---------------- DOWNLOAD ----------------

    public IEnumerator DownloadToFolder(FileData file, string folderPath)
    {
        string path = Path.Combine(folderPath, file.fileName);

        Directory.CreateDirectory(folderPath);

        UnityWebRequest request = UnityWebRequest.Get(file.url);
        request.downloadHandler = new DownloadHandlerFile(path);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Download error: " + request.error);
            yield break;
        }

        Debug.Log("Downloaded: " + path);
    }

    // ---------------- DOWNLOAD + EXTRACT ----------------

    public IEnumerator DownloadAndExtract(FileData file, string zipFolder, string extractFolder)
    {
        string zipPath = Path.Combine(zipFolder, file.fileName);

        Directory.CreateDirectory(zipFolder);

        UnityWebRequest request = UnityWebRequest.Get(file.url);
        request.downloadHandler = new DownloadHandlerFile(zipPath);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Download error: " + request.error);
            yield break;
        }

        if (Directory.Exists(extractFolder))
            Directory.Delete(extractFolder, true);

        Directory.CreateDirectory(extractFolder);

        ZipFile.ExtractToDirectory(zipPath, extractFolder);

        File.Delete(zipPath);

        Debug.Log("Extracted: " + extractFolder);
    }
}