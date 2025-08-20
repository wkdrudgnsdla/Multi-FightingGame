using System;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
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
    public GameObject panelsRoot; // Panels ��Ʈ (�ٸ� �ڵ尡 Start()���� ��Ȱ��ȭ�� �� ����)
    public GameObject hostPanel;   // ȣ��Ʈ ���� UI �г�
    public GameObject GuestPanel;  // �Խ�Ʈ ���� UI �г�

    [Header("����")]
    public int roomCodeLength = 8;
    public int maxPlayers = 2;

    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManagerComponent;
    private bool isHost = false; // �� �ν��Ͻ��� Host���� (���� �÷���)

    void Awake()
    {
        if (createBtn != null) createBtn.onClick.AddListener(() => CreateRoomAsync());
        if (joinBtn != null) joinBtn.onClick.AddListener(() => JoinRoomAsync(joinInput != null ? joinInput.text.Trim() : ""));

        AutoFindPanelsIfMissing();
        ApplyRoleUI(); // �ʱ� UI ���� (�⺻: guest view)
    }

    // �ڵ� Ž��: ��Ȱ�� ������Ʈ �����ؼ� ã��
    void AutoFindPanelsIfMissing()
    {
        if (panelsRoot == null)
        {
            panelsRoot = FindGameObjectAnywhereByName("Panels")
                      ?? FindGameObjectAnywhereByName("panels")
                      ?? FindGameObjectAnywhereByName("PanelsRoot")
                      ?? FindGameObjectAnywhereByName("panelsRoot");
            if (panelsRoot != null) Debug.Log($"[RoomManager] panelsRoot �ڵ� �Ҵ�: {panelsRoot.name}");
        }

        if (hostPanel == null)
        {
            hostPanel = FindGameObjectAnywhereByName("hostPanel")
                     ?? FindGameObjectAnywhereByName("HostPanel");
            if (hostPanel != null) Debug.Log($"[RoomManager] hostPanel �ڵ� �Ҵ�: {hostPanel.name}");
        }

        if (GuestPanel == null)
        {
            GuestPanel = FindGameObjectAnywhereByName("GuestPanel")
                       ?? FindGameObjectAnywhereByName("guestPanel")
                       ?? FindGameObjectAnywhereByName("ClientPanel")
                       ?? FindGameObjectAnywhereByName("clientpanel");
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

    // ===== ���� �߰�: ���� �� ��� ���� ���� =====
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
            // ������: runner�� null�̰ų� ī��Ʈ ���� ���� �� fallback
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

    async Task ShutdownRunner()
    {
        try
        {
            if (runner != null)
            {
                try { runner.Shutdown(false); } catch (Exception e) { Debug.LogWarning("Runner.Shutdown ����: " + e.Message); }
                await Task.Delay(100);
                Destroy(runner);
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
            // �߰� "�õ���" �޽��� ����(��û���)
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
            // �߰� �޽��� ����(��û���). ���� ���´� OnConnectedToServer �ݹ鿡�� ó��.
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

    // **�߿�**: Ŭ���̾�Ʈ/ȣ��Ʈ ���ο��� ������ ������ �̷���� ����
    public void OnConnectedToServer(NetworkRunner r)
    {
        Debug.Log("OnConnectedToServer ȣ���.");

        // ������ panels ���̰� �ϰ� UI ����
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

    // ���� ����(�ź� ����)
    public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason)
    {
        Debug.Log($"OnDisconnectedFromServer ȣ��. ����: {reason}");

        if (!isHost)
        {
            SetStatus("���� ����: ���� ���� �� �ְų� ȣ��Ʈ�� ������ �������ϴ�.");
            Debug.Log("Ŭ���̾�Ʈ: ���� �ź� �Ǵ� ���� ����");
            _ = ShutdownRunner();
        }
        else
        {
            SetStatus("ȣ��Ʈ ���� ����.");
            _ = ShutdownRunner();
            isHost = false;
            ApplyRoleUI();
        }
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
