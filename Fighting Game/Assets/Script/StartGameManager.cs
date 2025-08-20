using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Fusion; // NetworkRunner ã�� ����

public class StartGameManager : MonoBehaviour
{
    [Header("UI")]
    public Button exitButton;            // Inspector�� ExitRoom ��ư ����
    public float waitBeforeReload = 0.15f; // ���� �� ��� ��ٸ� �ð� (��)

    // ����θ� ���� ���� �ٽ� �ε�
    public string sceneNameToReload = "";

    void Start()
    {
        // �� �̸� �⺻�� ����
        if (string.IsNullOrEmpty(sceneNameToReload))
            sceneNameToReload = SceneManager.GetActiveScene().name;

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitRoomClicked);
        else
            Debug.LogWarning("[StartGameManager] exitButton�� ������� �ʾҽ��ϴ�. Inspector�� ��ư�� �־��ּ���.");
    }

    // ��ư �ݹ�
    public void OnExitRoomClicked()
    {
        // ��ư �ߺ� Ŭ�� ����
        if (exitButton != null)
            exitButton.interactable = false;

        StartCoroutine(ExitAndReloadCoroutine());
    }

    // �ڷ�ƾ: Runner ���� -> ��� ��� -> �� ��ε�
    IEnumerator ExitAndReloadCoroutine()
    {
        NetworkRunner runner = FindObjectOfType<NetworkRunner>();

        if (runner != null)
        {
            Debug.Log("[StartGameManager] NetworkRunner �߰� - ���� ���� �õ�");
            try
            {
                // Fusion API�� Shutdown ȣ�� (false�� ����)
                runner.Shutdown(false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[StartGameManager] runner.Shutdown ����: " + ex.Message);
            }

            // �����ϰ� ���� ��� (��Ʈ��ũ ���� �ð��� �ֱ� ����)
            yield return new WaitForSeconds(waitBeforeReload);
        }
        else
        {
            Debug.Log("[StartGameManager] NetworkRunner�� ã�� ����. �̹� ����Ǿ��ų� �ٸ� ��ü�� ���� ���� �� ����.");
        }

        // 2) �� ��ε�
        Debug.Log("[StartGameManager] �� ��ε�: " + sceneNameToReload);
        SceneManager.LoadScene(sceneNameToReload);

        yield break;
    }
}
