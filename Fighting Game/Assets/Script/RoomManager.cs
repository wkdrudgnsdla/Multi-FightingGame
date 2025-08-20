using System;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("UI ����")]
    public Text statusText;
    public Text roomCodeText;
    public InputField joinInput;
    public Button createBtn;
    public Button joinBtn;

    [Header("�г�")]
    public GameObject panelsRoot; // Panels ��Ʈ
    public GameObject hostPanel;   // ȣ��Ʈ ���� UI �г�
    public GameObject GuestPanel;  // �Խ�Ʈ ���� UI �г�

    [Header("����")]
    public int roomCodeLength = 8;
    public int maxPlayers = 2;

    [Header("���� ���� ����")]
    public bool ReturnToMainOnDisconnect = true; // �Խ�Ʈ�� ȣ��Ʈ ����� �ڵ����� �� �̵�����
    public string sceneToLoadOnExit = ""; // ��������� ���� ���� �ٽ� �ε�

    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManagerComponent;
    private bool isHost = false; // �� �ν��Ͻ��� Host���� (���� �÷���)
    private bool isExiting = false; // �� ��ȯ �ߺ� ����

    void Awake()
    {
        if (createBtn != null) createBtn.onClick.AddListener(() => CreateRoomAsync());
        if (joinBtn != null) joinBtn.onClick.AddListener(() => JoinRoomAsync(joinInput != null ? joinInput.text.Trim() : ""));

        FindPanels();
        ApplyRoleUI(); // �ʱ� UI ���� (�⺻: guest view)
    }

    void FindPanels()
    {
        if (panelsRoot == null)
        {
            panelsRoot = FindGameObjectAnywhereByName("Panels");
            if (panelsRoot != null) Debug.Log($"[RoomManager] panelsRoot �ڵ� �Ҵ�: {panelsRoot.name}");
        }

        if (hostPanel == null)
        {
            hostPanel = FindGameObjectAnywhereByName("HostPanel");
            if (hostPanel != null) Debug.Log($"[RoomManager] hostPanel �ڵ� �Ҵ�: {hostPanel.name}");
        }

        if (GuestPanel == null)
        {
            GuestPanel = FindGameObjectAnywhereByName("GuestPanel");
            if (GuestPanel != null) Debug.Log($"[RoomManager] GuestPanel �ڵ� �Ҵ�: {GuestPanel.name}");
        }

        if (panelsRoot == null)
            Debug.LogWarning("[RoomManager] panelsRoot�� �ڵ����� �Ҵ���� �ʾҽ��ϴ�. Inspector�� Panels ��Ʈ�� �Ҵ��ϼ���.");
        if (hostPanel == null)
            Debug.LogWarning("[RoomManager] hostPanel�� �ڵ����� �Ҵ���� �ʾҽ��ϴ�. Inspector�� HostPanel�� �����ϼ���.");
        if (GuestPanel == null)
            Debug.LogWarning("[RoomManager] GuestPanel�� �ڵ����� �Ҵ���� �ʾҽ��ϴ�. Inspector�� GuestPanel�� �����ϼ���.");
    }

    GameObject FindGameObjectAnywhereByName(string name)
    {
        try
        {
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (go != null && go.name == name)
                    return go;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[RoomManager] FindGameObjectAnywhereByName ����: " + e.Message);
        }
        return null;
    }

    // Panels ��Ʈ�� ��Ȱ�� ���¿��� ���� Ȱ��ȭ
    void EnsurePanelsVisible()
    {
        if (panelsRoot != null && !panelsRoot.activeSelf)
        {
            panelsRoot.SetActive(true);
            Debug.Log("[RoomManager] panelsRoot ���� Ȱ��ȭ");
        }
        else if (panelsRoot == null)
        {
            Debug.LogWarning("[RoomManager] EnsurePanelsVisible: panelsRoot�� �Ҵ�Ǿ� ���� �ʽ��ϴ�. Inspector�� �Ҵ��ϰų� ������Ʈ �̸��� Ȯ���ϼ���.");
        }
    }

    // UI ��� �� ����� �α� �߰�
    void ApplyRoleUI()
    {
        Debug.Log($"ApplyRoleUI ȣ��: isHost={isHost}, panelsRoot_active={(panelsRoot != null ? panelsRoot.activeSelf.ToString() : "null")}");
        if (hostPanel != null) hostPanel.SetActive(isHost);
        if (GuestPanel != null) GuestPanel.SetActive(!isHost);
        if (roomCodeText != null) roomCodeText.gameObject.SetActive(isHost);
    }

    void SetStatus(string msg)
    {
        Debug.Log("Status: " + msg);
        if (statusText != null) statusText.text = msg;
    }

    // ===== ���� �� ��� ���� ���� =====
    void UpdateStatusWithCount(NetworkRunner r)
    {
        if (r == null)
        {
            SetStatus($"���ӿϷ� 0/{maxPlayers}");
            return;
        }

        int count = 0;
        try
        {
            count = r.ActivePlayers.Count();
        }
        catch
        {
            if (runner != null)
                count = runner.ActivePlayers.Count();
        }

        count = Mathf.Clamp(count, 0, maxPlayers);
        SetStatus($"���ӿϷ� {count}/{maxPlayers}");
    }

    string GenerateRoomCode(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);
        return sb.ToString();
    }

    // ���� �񵿱� ���� �޼��� 
    async Task ShutdownRunner()
    {
        try
        {
            if (runner != null)
            {
                try { runner.Shutdown(false); }
                catch (Exception e) { Debug.LogWarning("Runner.Shutdown ����: " + e.Message); }

                await Task.Delay(100);
                try { Destroy(runner); } catch { }
                runner = null;
                Debug.Log("Runner ���� �� ���� �Ϸ�");
            }
        }
        catch (Exception ex) { Debug.LogWarning("ShutdownRunner ����: " + ex.Message); }
    }

    // Host ����: panels ���� Ȱ��ȭ �� UI ��ȯ
    public async void CreateRoomAsync()
    {
        EnsurePanelsVisible();

        isHost = true;
        ApplyRoleUI();

        if (runner != null) await ShutdownRunner();

        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        if (buildIndex < 0)
        {
            SetStatus("����: ���� ���� Build Settings�� ��ϵǾ� ���� �ʽ��ϴ�.");
            Debug.LogWarning("Build Settings�� ���� ���� �߰��ϼ��� (File > Build Settings > Add Open Scenes).");
            isHost = false;
            ApplyRoleUI();
            return;
        }

        var sceneRef = SceneRef.FromIndex(buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);

        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        if (sceneManagerComponent == null)
            sceneManagerComponent = gameObject.AddComponent<NetworkSceneManagerDefault>();

        const int maxTries = 5;
        bool created = false;
        for (int attempt = 0; attempt < maxTries; attempt++)
        {
            string code = GenerateRoomCode(roomCodeLength);
            Debug.Log($"�� ���� �õ�: {code}");

            var args = new StartGameArgs
            {
                GameMode = GameMode.Host,
                SessionName = code,
                Scene = sceneInfo,
                SceneManager = sceneManagerComponent
            };

            try
            {
                await runner.StartGame(args);

                if (runner != null && runner.IsRunning)
                {
                    created = true;
                    if (roomCodeText != null) roomCodeText.text = code;
                    Debug.Log($"�� ���� ����: {code}");

                    // ���� ���� ���� ���� (ȣ��Ʈ �ڽ� ����)
                    UpdateStatusWithCount(runner);
                    break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"StartGame ����: {e.Message}");
            }

            if (runner != null) { Destroy(runner); runner = null; }
            await Task.Delay(200);
        }

        if (!created)
        {
            SetStatus("�� ���� ���� (�ߺ� ���� �ذ� �Ұ�)");
            Debug.LogWarning("�� ������ �����߽��ϴ�.");
            isHost = false;
            ApplyRoleUI();
            if (runner != null) { await ShutdownRunner(); }
        }
    }

    // Guest: ���� �õ� (���� UI ������ OnConnectedToServer���� ó��)
    public async void JoinRoomAsync(string code)
    {
        code = (code ?? "").Trim();
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("������ �� �ڵ带 �Է��ϼ���.");
            return;
        }

        isHost = false;
        ApplyRoleUI();

        if (runner != null) await ShutdownRunner();

        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        var args = new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = code,
            SceneManager = sceneManagerComponent
        };

        try
        {
            await runner.StartGame(args);
            Debug.Log($"Join StartGame ȣ��: {code}");
            // ���� ���´� OnConnectedToServer �ݹ鿡�� ó��.
        }
        catch (Exception e)
        {
            SetStatus($"���� ����: {e.Message}");
            Debug.LogWarning($"Join StartGame ����: {e.Message}");
            if (runner != null) { Destroy(runner); runner = null; }
        }
    }

    // ============================
    // INetworkRunnerCallbacks
    // ============================

    // Host: �� �÷��̾� ���� ���� -> ���� �ʰ��� ���� ����
    public void OnPlayerJoined(NetworkRunner r, PlayerRef player)
    {
        int count = r.ActivePlayers.Count();
        Debug.Log($"[Host] �÷��̾� ���� ����. ���� �ο�: {count}");

        // ������ �� ���� (ȣ��Ʈ ȭ��)
        UpdateStatusWithCount(r);

        if (count > maxPlayers)
        {
            Debug.Log($"���� �ʰ�: {count} > {maxPlayers}. �ش� �÷��̾� ���� �����ŵ�ϴ�.");
            r.Disconnect(player);
        }
    }

    public void OnPlayerLeft(NetworkRunner r, PlayerRef player)
    {
        Debug.Log("[Host] �÷��̾� ���� ����.");
        UpdateStatusWithCount(r);
    }

    // Ŭ���̾�Ʈ/ȣ��Ʈ ���ο��� ������ ������ �̷���� ����
    public void OnConnectedToServer(NetworkRunner r)
    {
        Debug.Log("OnConnectedToServer ȣ���.");

        EnsurePanelsVisible();

        // ���� ������ Ȯ���� �������� ������ ���� ���� ����
        UpdateStatusWithCount(r);

        if (!isHost)
        {
            Debug.Log("Ŭ���̾�Ʈ: ���� �Ϸ�, GuestPanel Ȱ��ȭ");
            ApplyRoleUI();                // GuestPanel Ȱ��ȭ
        }
        else
        {
            Debug.Log("ȣ��Ʈ: OnConnectedToServer - ���� ���� ��");
            ApplyRoleUI();
        }
    }

    // ���� ����
    public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason)
    {
        Debug.Log($"OnDisconnectedFromServer ȣ��. ����: {reason}");

        if (!isHost)
        {
            SetStatus("���� ����: ���� ���� �� �ְų� ȣ��Ʈ�� ������ �������ϴ�.");
            Debug.Log("Ŭ���̾�Ʈ: ���� �ź� �Ǵ� ���� ����");

            _ = ShutdownRunner();

            // �ڵ����� �� �̵����� ����
            if (ReturnToMainOnDisconnect && !isExiting)
            {
                StartCoroutine(LoadSceneOnDisconnectCoroutine());
            }
        }
        else
        {
            // ȣ��Ʈ�� �ڽ��� ������ ������ ���
            SetStatus("ȣ��Ʈ ���� ����.");
            _ = ShutdownRunner();
            isHost = false;
            ApplyRoleUI();
        }
    }

    // �Խ�Ʈ�� �ڵ����� ���ư� �� ����ϴ� �ڷ�ƾ
    IEnumerator LoadSceneOnDisconnectCoroutine()
    {
        isExiting = true;
        yield return new WaitForSeconds(0.15f);

        string targetScene = string.IsNullOrEmpty(sceneToLoadOnExit)
            ? SceneManager.GetActiveScene().name
            : sceneToLoadOnExit;

        Debug.Log("[RoomManager] �Խ�Ʈ ���� ���� -> �� �̵�: " + targetScene);
        SceneManager.LoadScene(targetScene);
    }

    // ----------------------------
    // ExitRoom: ��ư���� ȣ���Ͽ� ���� ������ �����ϰ� �����ϰ� �� ���ε�
    // ----------------------------
    public void ExitRoom()
    {
        if (isExiting) return;
        StartCoroutine(ExitRoomCoroutine());
    }

    private IEnumerator ExitRoomCoroutine()
    {
        isExiting = true;

        // 1) ȣ��Ʈ�̸� ���� ���ӵ� ��� �÷��̾�(�Խ�Ʈ)�� ������ Disconnect �õ�
        if (isHost && runner != null)
        {
            try
            {
                // ActivePlayers�� �����ؼ� �����ϰ� ��ȸ
                var players = runner.ActivePlayers.ToList();
                Debug.Log($"[RoomManager] ExitRoom: ȣ��Ʈ�� {players.Count}�� ���� disconnect �õ�");
                foreach (var p in players)
                {
                    try
                    {
                        runner.Disconnect(p);
                        Debug.Log($"[RoomManager] ExitRoom: Disconnect ȣ�� - PlayerRef {p}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[RoomManager] ExitRoom Disconnect ����: " + e.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RoomManager] ExitRoom: �÷��̾� ���� Disconnect �� ����: " + ex.Message);
            }
        }

        if (runner != null)
        {
            try
            {
                runner.Shutdown(false);
                Debug.Log("[RoomManager] ExitRoom: runner.Shutdown ȣ��");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoomManager] ExitRoom runner.Shutdown ����: " + e.Message);
            }
        }
        else
        {
            Debug.Log("[RoomManager] ExitRoom: runner ����");
        }

        yield return new WaitForSeconds(0.15f);

        string targetScene = string.IsNullOrEmpty(sceneToLoadOnExit)
            ? SceneManager.GetActiveScene().name
            : sceneToLoadOnExit;

        Debug.Log("[RoomManager] ExitRoom: �� �̵� -> " + targetScene);
        SceneManager.LoadScene(targetScene);
    }

    // ������ �ݹ��(������� ������ �� ����)
    public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest req, byte[] token) { }
    public void OnConnectFailed(NetworkRunner r, NetAddress address, NetConnectFailedReason reason) { Debug.LogWarning("OnConnectFailed: " + reason); }
    public void OnInput(NetworkRunner r, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner r, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner r, ShutdownReason reason) { Debug.Log("Runner Shutdown: " + reason); }
    public void OnHostMigration(NetworkRunner r, HostMigrationToken token) { Debug.Log("Host migration"); }
    public void OnReliableDataReceived(NetworkRunner r, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner r) { }
    public void OnSceneLoadDone(NetworkRunner r) { }
    public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
    public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
    public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr msg) { }

#if UNITY_EDITOR
    void OnValidate()
    {
        roomCodeLength = Mathf.Clamp(roomCodeLength, 4, 12);
        if (maxPlayers < 1) maxPlayers = 1;
    }
#endif
}
