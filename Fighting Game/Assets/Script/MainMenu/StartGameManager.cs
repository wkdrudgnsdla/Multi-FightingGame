using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.UI;

public class StartGameManager : NetworkBehaviour
{
    [Header("UI")]
    public Button readyButton;        // ���� �÷��̾ ������ Ready ��ư
    public Text readyButtonText;      // Ready / Cancel ǥ�ÿ�
    public Button startButton;        // ȣ��Ʈ ���� Start ��ư (ȣ��Ʈ�� Ȱ��ȭ)
    public Text hostReadyText;        // ȣ��Ʈ ȭ�鿡 ȣ��Ʈ �غ� ���� ǥ�� (����)
    public Text guestReadyText;       // ȣ��Ʈ ȭ�鿡 �Խ�Ʈ �غ� ���� ǥ�� (����)
    public Text statusText;           // ���� ���� �޽���

    [Header("����")]
    public int maxPlayers = 2;

    private List<PlayerRef> readyPlayers = new List<PlayerRef>();

    // Networked �ʵ�: Fusion ������ �°� �⺻�� ���
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
            // RpcInfo �ڸ����� default�� �־� ȣ�� (Ŭ���̾�Ʈ���� ȣ�� �� ������ ����)
            RPC_SetReady(default, localReady);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[StartGameManager] RPC_SetReady ȣ�� ����: " + e.Message);
        }
    }

    public void OnStartButtonClicked()
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[StartGameManager] OnStartButtonClicked: ������ StateAuthority(Host)�� �ƴ�.");
            return;
        }

        if (!AllReady)
        {
            Debug.Log("[StartGameManager] ���� ��� �غ� ���� ����.");
            return;
        }

        // StateAuthority���� ��� Ŭ���̾�Ʈ�� Start ��ȣ ����
        RPC_StartGame(default);
    }

    // Ŭ���̾�Ʈ -> Host(StateAuthority)
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

        // Host�� ��� Ŭ���̾�Ʈ���� ���� ���¸� ��ε�ĳ��Ʈ (ù ���ڿ� default)
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

    // Host(StateAuthority) -> All : ���� ���� ��ȣ
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_StartGame(RpcInfo info)
    {
        Debug.Log("[StartGameManager] RPC_StartGame ȣ�� - ���� ���� ��ȣ ����");
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
