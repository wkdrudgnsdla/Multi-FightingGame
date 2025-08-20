using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Fusion; // NetworkRunner 찾기 위해

public class StartGameManager : MonoBehaviour
{
    [Header("UI")]
    public Button exitButton;            // Inspector에 ExitRoom 버튼 연결
    public float waitBeforeReload = 0.15f; // 종료 후 잠깐 기다릴 시간 (초)

    // 비워두면 현재 씬을 다시 로드
    public string sceneNameToReload = "";

    void Start()
    {
        // 씬 이름 기본값 설정
        if (string.IsNullOrEmpty(sceneNameToReload))
            sceneNameToReload = SceneManager.GetActiveScene().name;

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitRoomClicked);
        else
            Debug.LogWarning("[StartGameManager] exitButton이 연결되지 않았습니다. Inspector에 버튼을 넣어주세요.");
    }

    // 버튼 콜백
    public void OnExitRoomClicked()
    {
        // 버튼 중복 클릭 방지
        if (exitButton != null)
            exitButton.interactable = false;

        StartCoroutine(ExitAndReloadCoroutine());
    }

    // 코루틴: Runner 종료 -> 잠깐 대기 -> 씬 재로드
    IEnumerator ExitAndReloadCoroutine()
    {
        NetworkRunner runner = FindObjectOfType<NetworkRunner>();

        if (runner != null)
        {
            Debug.Log("[StartGameManager] NetworkRunner 발견 - 세션 종료 시도");
            try
            {
                // Fusion API의 Shutdown 호출 (false로 예시)
                runner.Shutdown(false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[StartGameManager] runner.Shutdown 예외: " + ex.Message);
            }

            // 안전하게 조금 대기 (네트워크 정리 시간을 주기 위해)
            yield return new WaitForSeconds(waitBeforeReload);
        }
        else
        {
            Debug.Log("[StartGameManager] NetworkRunner를 찾지 못함. 이미 종료되었거나 다른 객체가 관리 중일 수 있음.");
        }

        // 2) 씬 재로드
        Debug.Log("[StartGameManager] 씬 재로드: " + sceneNameToReload);
        SceneManager.LoadScene(sceneNameToReload);

        yield break;
    }
}
