using UnityEngine;

public class MainMenuUIManager : MonoBehaviour
{
    public GameObject MainMenuUI;
    public GameObject SelectRoomUI;
    public GameObject Panels;

    private void Awake()
    {
        MainMenuUI = GameObject.Find("MainMenuUI");
        SelectRoomUI = GameObject.Find("SelectRoomUI");
        Panels = GameObject.Find("Panels");
    }

    private void Start()
    {
        SelectRoomUI.SetActive(false);
        Panels.SetActive(false);
    }

    public void OnClickStartButton()
    {
        MainMenuUI.SetActive(false);
        SelectRoomUI.SetActive(true);
    }
}
