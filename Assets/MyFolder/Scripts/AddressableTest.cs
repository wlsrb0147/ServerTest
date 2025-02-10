using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Video;

public class AddressableTest : MonoBehaviour
{
    // 에디터에서 할당할 VideoClip 타입의 Addressable Asset 참조
    public AssetReference videoClipReference;
    public VideoPlayer videoPlayer;

    private void Start()
    {
        // Addressables 초기화 후 카탈로그 업데이트 체크
        Addressables.InitializeAsync().Completed += handle =>
        {
            CheckAndUpdateCatalogs();
        };
    }

    private void CheckAndUpdateCatalogs()
    {
        // 원격 카탈로그의 업데이트가 있는지 확인
        Addressables.CheckForCatalogUpdates(false).Completed += updateHandle =>
        {
            if (updateHandle.Status == AsyncOperationStatus.Succeeded && updateHandle.Result.Count > 0)
            {
                // 업데이트된 카탈로그가 있으면 갱신
                Addressables.UpdateCatalogs(updateHandle.Result).Completed += updateCatalogHandle =>
                {
                    LoadAndPlayVideo();
                };
            }
            else
            {
                LoadAndPlayVideo();
            }
        };
    }

    private void LoadAndPlayVideo()
    {
        // 해당 자산에 대한 모든 의존성을 다운로드 (이미 캐시되어 있으면 재다운로드하지 않음)
        Addressables.DownloadDependenciesAsync(videoClipReference, true).Completed += downloadHandle =>
        {
            if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                // 비디오 자산 로드 후 VideoPlayer에 할당
                Addressables.LoadAssetAsync<VideoClip>(videoClipReference).Completed += loadHandle =>
                {
                    if (loadHandle.Status == AsyncOperationStatus.Succeeded)
                    {
                        videoPlayer.clip = loadHandle.Result;
                        videoPlayer.Play();
                    }
                    else
                    {
                        Debug.LogError("비디오 로드 실패");
                    }
                };
            }
            else
            {
                Debug.LogError("비디오 다운로드 실패");
            }
        };
    }
}
