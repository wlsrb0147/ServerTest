using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

public class VideoManager : MonoBehaviour
{
    private string serverUrl = "http://211.110.44.104:8500/resource/";
    private string fileName = "205.mp4";
    private string localPath;
    public VideoPlayer videoPlayer;

    private void Start()
    {
        localPath = Path.Combine(Application.persistentDataPath, fileName);
        serverUrl = string.Concat(serverUrl, fileName);
        CheckAndDownloadVideo(videoPlayer,localPath,serverUrl).Forget();
    }

    private async UniTaskVoid CheckAndDownloadVideo(VideoPlayer player, string localFilePath, string videoUrl)
    {
        try
        {
            if (File.Exists(localFilePath))
            {
                Debug.Log("파일이 존재함, 해시값 비교 시작");

                string localHash = await GetFileHashAsync(localFilePath);
                string remoteHash = await GetRemoteFileHash(videoUrl);

                if (!string.IsNullOrEmpty(remoteHash) && localHash == remoteHash)
                {
                    Debug.Log("파일이 동일함, 바로 실행");
                    SaveVideo(player,localFilePath);
                    return;
                }

                Debug.Log("파일이 변경되었거나 해시 비교 실패, 기존 파일 삭제 후 다운로드 진행");
                File.Delete(localFilePath);
            }
            else
            {
                Debug.Log("파일이 존재하지 않음, 다운로드 진행");
            }

            await DownloadVideo(videoUrl, localFilePath);
            SaveVideo(player,localFilePath);
        }
        catch (Exception ex)
        {
            Debug.LogError("비디오 처리 중 예외 발생: " + ex.Message);
        }
    }

    // 원격 파일의 해시값을 가져오기 위한 메서드
    private async UniTask<string> GetRemoteFileHash(string url)
    {
        // HEAD 요청 시도
        // HTTP의 헤더 정보만 반환
        // using = 사용종료 후 자동으로 dispose를 함
        using (UnityWebRequest headRequest = UnityWebRequest.Head(url))
        {
            await headRequest.SendWebRequest();

            if (headRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("HEAD 요청 성공.");

                // Content-MD5 헤더가 있을 경우 (보통 Base64 인코딩)
                string contentMd5 = headRequest.GetResponseHeader("Content-MD5");
                if (!string.IsNullOrEmpty(contentMd5))
                {
                    try
                    {
                        byte[] md5Bytes = Convert.FromBase64String(contentMd5);
                        string hex = BitConverter.ToString(md5Bytes).Replace("-", "").ToLower();
                        return hex;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("Content-MD5 헤더 변환 실패: " + ex.Message);
                    }
                }

                // ETag 헤더가 MD5 해시를 포함할 가능성이 있음 (따옴표 제거 후 정규표현식 검사)
                string etag = headRequest.GetResponseHeader("ETag");
                if (!string.IsNullOrEmpty(etag))
                {
                    etag = etag.Trim('\"');
                    if (etag.Length == 32 && Regex.IsMatch(etag, @"\A\b[0-9a-fA-F]+\b\Z"))
                    {
                        Debug.Log("HasETag");
                        return etag.ToLower();
                    }
                }
            }
            else
            {
                Debug.LogWarning("HEAD 요청 실패 (대체 GET 요청 진행): " + headRequest.error);
            }
        }

        // HEAD 요청으로 해시를 얻을 수 없으므로, GET 요청으로 전체 파일 다운로드 후 해시 계산
        using (UnityWebRequest getRequest = UnityWebRequest.Get(url))
        {
            await getRequest.SendWebRequest();

            if (getRequest.result == UnityWebRequest.Result.Success)
            {
                byte[] fileData = getRequest.downloadHandler.data;
                return ComputeMD5Hash(fileData);
            }
            else
            {
                Debug.LogError("원격 파일 해시값 가져오기 실패: " + getRequest.error);
                return string.Empty;
            }
        }
    }

    // 로컬 파일의 MD5 해시를 스트리밍 방식으로 계산하여 메모리 사용을 줄임
    private async UniTask<string> GetFileHashAsync(string filePath)
    {
        return await UniTask.RunOnThreadPool(() =>
        {
            using FileStream stream = File.OpenRead(filePath);
            using MD5 md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        });
    }

    // 바이트 배열에서 MD5 해시를 계산 (원격 파일 GET 요청 결과 사용)
    private string ComputeMD5Hash(byte[] data)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] hashBytes = md5.ComputeHash(data);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }

    // DownloadHandlerFile을 사용하여 비디오 파일을 저장 (네트워크 오류에 대비해 예외 처리를 고려)
    private async UniTask DownloadVideo(string url, string savePath)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.downloadHandler = new DownloadHandlerFile(savePath);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("비디오 다운로드 실패: " + request.error);
            }
            else
            {
                Debug.Log("비디오 다운로드 완료: " + savePath);
            }
        }
    }

    // VideoPlayer에 URL을 할당하고 재생 시작
    private void SaveVideo(VideoPlayer saveVideo, string path)
    {
        saveVideo.url = path;
    }
}
