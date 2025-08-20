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
    [Header("UI 연결")]
    public Text statusText;
    public Text roomCodeText;
    public InputField joinInput;
    public Button createBtn;
    public Button joinBtn;

    [Header("패널")]
    public GameObject panelsRoot; // Panels 루트
    public GameObject hostPanel;   // 호스트 전용 UI 패널
    public GameObject GuestPanel;  // 게스트 전용 UI 패널

    [Header("설정")]
    public int roomCodeLength = 8;
    public int maxPlayers = 2;

    [Header("종료 동작 설정")]
    public bool ReturnToMainOnDisconnect = true; // 게스트가 호스트 종료시 자동으로 씬 이동할지
    public string sceneToLoadOnExit = ""; // 비어있으면 현재 씬을 다시 로드

    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManagerComponent;
    private bool isHost = false; // 이 인스턴스가 Host인지 (로컬 플래그)
    private bool isExiting = false; // 씬 전환 중복 방지

    void Awake()
    {
        if (createBtn != null) createBtn.onClick.AddListener(() => CreateRoomAsync());
        if (joinBtn != null) joinBtn.onClick.AddListener(() => JoinRoomAsync(joinInput != null ? joinInput.text.Trim() : ""));

        FindPanels();
        ApplyRoleUI(); // 초기 UI 적용 (기본: guest view)
    }

    void FindPanels()
    {
        if (panelsRoot == null)
        {
            panelsRoot = FindGameObjectAnywhereByName("Panels");
            if (panelsRoot != null) Debug.Log($"[RoomManager] panelsRoot 자동 할당: {panelsRoot.name}");
        }

        if (hostPanel == null)
        {
            hostPanel = FindGameObjectAnywhereByName("HostPanel");
            if (hostPanel != null) Debug.Log($"[RoomManager] hostPanel 자동 할당: {hostPanel.name}");
        }

        if (GuestPanel == null)
        {
            GuestPanel = FindGameObjectAnywhereByName("GuestPanel");
            if (GuestPanel != null) Debug.Log($"[RoomManager] GuestPanel 자동 할당: {GuestPanel.name}");
        }

        if (panelsRoot == null)
            Debug.LogWarning("[RoomManager] panelsRoot가 자동으로 할당되지 않았습니다. Inspector에 Panels 루트를 할당하세요.");
        if (hostPanel == null)
            Debug.LogWarning("[RoomManager] hostPanel이 자동으로 할당되지 않았습니다. Inspector에 HostPanel을 연결하세요.");
        if (GuestPanel == null)
            Debug.LogWarning("[RoomManager] GuestPanel이 자동으로 할당되지 않았습니다. Inspector에 GuestPanel을 연결하세요.");
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
            Debug.LogWarning("[RoomManager] FindGameObjectAnywhereByName 실패: " + e.Message);
        }
        return null;
    }

    // Panels 루트가 비활성 상태여도 강제 활성화
    void EnsurePanelsVisible()
    {
        if (panelsRoot != null && !panelsRoot.activeSelf)
        {
            panelsRoot.SetActive(true);
            Debug.Log("[RoomManager] panelsRoot 강제 활성화");
        }
        else if (panelsRoot == null)
        {
            Debug.LogWarning("[RoomManager] EnsurePanelsVisible: panelsRoot가 할당되어 있지 않습니다. Inspector에 할당하거나 오브젝트 이름을 확인하세요.");
        }
    }

    // UI 토글 및 디버그 로그 추가
    void ApplyRoleUI()
    {
        Debug.Log($"ApplyRoleUI 호출: isHost={isHost}, panelsRoot_active={(panelsRoot != null ? panelsRoot.activeSelf.ToString() : "null")}");
        if (hostPanel != null) hostPanel.SetActive(isHost);
        if (GuestPanel != null) GuestPanel.SetActive(!isHost);
        if (roomCodeText != null) roomCodeText.gameObject.SetActive(isHost);
    }

    void SetStatus(string msg)
    {
        Debug.Log("Status: " + msg);
        if (statusText != null) statusText.text = msg;
    }

    // ===== 접속 수 기반 상태 갱신 =====
    void UpdateStatusWithCount(NetworkRunner r)
    {
        if (r == null)
        {
            SetStatus($"접속완료 0/{maxPlayers}");
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
        SetStatus($"접속완료 {count}/{maxPlayers}");
    }

    string GenerateRoomCode(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);
        return sb.ToString();
    }

    // 기존 비동기 정리 메서드 
    async Task ShutdownRunner()
    {
        try
        {
            if (runner != null)
            {
                try { runner.Shutdown(false); }
                catch (Exception e) { Debug.LogWarning("Runner.Shutdown 예외: " + e.Message); }

                await Task.Delay(100);
                try { Destroy(runner); } catch { }
                runner = null;
                Debug.Log("Runner 종료 및 정리 완료");
            }
        }
        catch (Exception ex) { Debug.LogWarning("ShutdownRunner 예외: " + ex.Message); }
    }

    // Host 생성: panels 강제 활성화 후 UI 전환
    public async void CreateRoomAsync()
    {
        EnsurePanelsVisible();

        isHost = true;
        ApplyRoleUI();

        if (runner != null) await ShutdownRunner();

        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        if (buildIndex < 0)
        {
            SetStatus("오류: 현재 씬이 Build Settings에 등록되어 있지 않습니다.");
            Debug.LogWarning("Build Settings에 현재 씬을 추가하세요 (File > Build Settings > Add Open Scenes).");
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
            Debug.Log($"방 생성 시도: {code}");

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
                    Debug.Log($"방 생성 성공: {code}");

                    // 생성 직후 상태 갱신 (호스트 자신 포함)
                    UpdateStatusWithCount(runner);
                    break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"StartGame 실패: {e.Message}");
            }

            if (runner != null) { Destroy(runner); runner = null; }
            await Task.Delay(200);
        }

        if (!created)
        {
            SetStatus("방 생성 실패 (중복 문제 해결 불가)");
            Debug.LogWarning("방 생성에 실패했습니다.");
            isHost = false;
            ApplyRoleUI();
            if (runner != null) { await ShutdownRunner(); }
        }
    }

    // Guest: 접속 시도 (최종 UI 갱신은 OnConnectedToServer에서 처리)
    public async void JoinRoomAsync(string code)
    {
        code = (code ?? "").Trim();
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("입장할 방 코드를 입력하세요.");
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
            Debug.Log($"Join StartGame 호출: {code}");
            // 최종 상태는 OnConnectedToServer 콜백에서 처리.
        }
        catch (Exception e)
        {
            SetStatus($"입장 실패: {e.Message}");
            Debug.LogWarning($"Join StartGame 예외: {e.Message}");
            if (runner != null) { Destroy(runner); runner = null; }
        }
    }

    // ============================
    // INetworkRunnerCallbacks
    // ============================

    // Host: 새 플레이어 접속 감지 -> 정원 초과면 강제 퇴장
    public void OnPlayerJoined(NetworkRunner r, PlayerRef player)
    {
        int count = r.ActivePlayers.Count();
        Debug.Log($"[Host] 플레이어 접속 감지. 현재 인원: {count}");

        // 접속자 수 갱신 (호스트 화면)
        UpdateStatusWithCount(r);

        if (count > maxPlayers)
        {
            Debug.Log($"정원 초과: {count} > {maxPlayers}. 해당 플레이어 강제 퇴장시킵니다.");
            r.Disconnect(player);
        }
    }

    public void OnPlayerLeft(NetworkRunner r, PlayerRef player)
    {
        Debug.Log("[Host] 플레이어 퇴장 감지.");
        UpdateStatusWithCount(r);
    }

    // 클라이언트/호스트 전부에서 연결이 완전히 이루어진 시점
    public void OnConnectedToServer(NetworkRunner r)
    {
        Debug.Log("OnConnectedToServer 호출됨.");

        EnsurePanelsVisible();

        // 최종 연결이 확정된 시점에서 접속자 수로 상태 갱신
        UpdateStatusWithCount(r);

        if (!isHost)
        {
            Debug.Log("클라이언트: 입장 완료, GuestPanel 활성화");
            ApplyRoleUI();                // GuestPanel 활성화
        }
        else
        {
            Debug.Log("호스트: OnConnectedToServer - 세션 실행 중");
            ApplyRoleUI();
        }
    }

    // 연결 끊김
    public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason)
    {
        Debug.Log($"OnDisconnectedFromServer 호출. 이유: {reason}");

        if (!isHost)
        {
            SetStatus("입장 실패: 방이 가득 차 있거나 호스트가 연결을 끊었습니다.");
            Debug.Log("클라이언트: 입장 거부 또는 연결 끊김");

            _ = ShutdownRunner();

            // 자동으로 씬 이동할지 결정
            if (ReturnToMainOnDisconnect && !isExiting)
            {
                StartCoroutine(LoadSceneOnDisconnectCoroutine());
            }
        }
        else
        {
            // 호스트가 자신의 세션을 종료한 경우
            SetStatus("호스트 연결 종료.");
            _ = ShutdownRunner();
            isHost = false;
            ApplyRoleUI();
        }
    }

    // 게스트가 자동으로 돌아갈 때 사용하는 코루틴
    IEnumerator LoadSceneOnDisconnectCoroutine()
    {
        isExiting = true;
        yield return new WaitForSeconds(0.15f);

        string targetScene = string.IsNullOrEmpty(sceneToLoadOnExit)
            ? SceneManager.GetActiveScene().name
            : sceneToLoadOnExit;

        Debug.Log("[RoomManager] 게스트 연결 끊김 -> 씬 이동: " + targetScene);
        SceneManager.LoadScene(targetScene);
    }

    // ----------------------------
    // ExitRoom: 버튼에서 호출하여 현재 세션을 안전하게 종료하고 씬 리로드
    // ----------------------------
    public void ExitRoom()
    {
        if (isExiting) return;
        StartCoroutine(ExitRoomCoroutine());
    }

    private IEnumerator ExitRoomCoroutine()
    {
        isExiting = true;

        // 1) 호스트이면 현재 접속된 모든 플레이어(게스트)를 강제로 Disconnect 시도
        if (isHost && runner != null)
        {
            try
            {
                // ActivePlayers를 복사해서 안전하게 순회
                var players = runner.ActivePlayers.ToList();
                Debug.Log($"[RoomManager] ExitRoom: 호스트가 {players.Count}명 강제 disconnect 시도");
                foreach (var p in players)
                {
                    try
                    {
                        runner.Disconnect(p);
                        Debug.Log($"[RoomManager] ExitRoom: Disconnect 호출 - PlayerRef {p}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[RoomManager] ExitRoom Disconnect 예외: " + e.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RoomManager] ExitRoom: 플레이어 강제 Disconnect 중 예외: " + ex.Message);
            }
        }

        if (runner != null)
        {
            try
            {
                runner.Shutdown(false);
                Debug.Log("[RoomManager] ExitRoom: runner.Shutdown 호출");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RoomManager] ExitRoom runner.Shutdown 예외: " + e.Message);
            }
        }
        else
        {
            Debug.Log("[RoomManager] ExitRoom: runner 없음");
        }

        yield return new WaitForSeconds(0.15f);

        string targetScene = string.IsNullOrEmpty(sceneToLoadOnExit)
            ? SceneManager.GetActiveScene().name
            : sceneToLoadOnExit;

        Debug.Log("[RoomManager] ExitRoom: 씬 이동 -> " + targetScene);
        SceneManager.LoadScene(targetScene);
    }

    // 나머지 콜백들(사용하지 않으면 빈 구현)
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
