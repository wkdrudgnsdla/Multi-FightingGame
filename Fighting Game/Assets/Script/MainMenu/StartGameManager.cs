using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.UI;

public class StartGameManager : NetworkBehaviour
{
    [Header("UI")]
    public Button readyButton;        // 로컬 플레이어가 누르는 Ready 버튼
    public Text readyButtonText;      // Ready / Cancel 표시용
    public Button startButton;        // 호스트 전용 Start 버튼 (호스트만 활성화)
    public Text hostReadyText;        // 호스트 화면에 호스트 준비 여부 표시 (예시)
    public Text guestReadyText;       // 호스트 화면에 게스트 준비 여부 표시 (예시)
    public Text statusText;           // 간단 상태 메시지

    [Header("설정")]
    public int maxPlayers = 2;

    private List<PlayerRef> readyPlayers = new List<PlayerRef>();

    // Networked 필드: Fusion 버전에 맞게 기본형 사용
    [Networked]
    public int ReadyCount { get; set; }

    [Networked]
    public bool AllReady { get; set; }

    private bool localReady = false;
    public event Action OnStartConfirmedByHost;

    void Start()
    {
        if (readyButton != null)
        {
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        }
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartButtonClicked);
            startButton.interactable = false;
        }

        UpdateLocalUI();
    }

    void UpdateLocalUI()
    {
        if (readyButtonText != null)
            readyButtonText.text = localReady ? "Cancel Ready" : "Ready";

        if (statusText != null)
            statusText.text = $"ReadyCount: {ReadyCount}/{maxPlayers}  AllReady: {AllReady}";
    }

    public void OnReadyButtonClicked()
    {
        localReady = !localReady;
        UpdateLocalUI();

        try
        {
            // RpcInfo 자리에는 default를 넣어 호출 (클라이언트에서 호출 시 안전한 패턴)
            RPC_SetReady(default, localReady);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[StartGameManager] RPC_SetReady 호출 예외: " + e.Message);
        }
    }

    public void OnStartButtonClicked()
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[StartGameManager] OnStartButtonClicked: 로컬이 StateAuthority(Host)가 아님.");
            return;
        }

        if (!AllReady)
        {
            Debug.Log("[StartGameManager] 아직 모든 준비가 되지 않음.");
            return;
        }

        // StateAuthority에서 모든 클라이언트로 Start 신호 전송
        RPC_StartGame(default);
    }

    // 클라이언트 -> Host(StateAuthority)
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetReady(RpcInfo info, bool ready)
    {
        if (!Object.HasStateAuthority)
            return;

        PlayerRef sender = info.Source;

        if (ready)
        {
            if (!readyPlayers.Contains(sender))
                readyPlayers.Add(sender);
        }
        else
        {
            if (readyPlayers.Contains(sender))
                readyPlayers.Remove(sender);
        }

        ReadyCount = readyPlayers.Count;
        AllReady = (ReadyCount >= maxPlayers);

        // Host가 모든 클라이언트에게 현재 상태를 브로드캐스트 (첫 인자에 default)
        RPC_BroadcastState(default, ReadyCount, AllReady);
    }

    // Host(StateAuthority) -> All
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_BroadcastState(RpcInfo info, int readyCount, bool allReady)
    {
        ReadyCount = readyCount;
        AllReady = allReady;

        UpdateLocalUI();

        if (Object.HasStateAuthority && startButton != null)
        {
            startButton.interactable = AllReady;
        }
    }

    // Host(StateAuthority) -> All : 게임 시작 신호
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_StartGame(RpcInfo info)
    {
        Debug.Log("[StartGameManager] RPC_StartGame 호출 - 게임 시작 신호 수신");
        OnStartConfirmedByHost?.Invoke();
    }

    public override void Spawned()
    {
        base.Spawned();
        if (Object.HasStateAuthority)
        {
            readyPlayers.Clear();
            ReadyCount = 0;
            AllReady = false;
        }

        UpdateLocalUI();
    }

    public void RefreshLocalUI()
    {
        UpdateLocalUI();
    }
}
