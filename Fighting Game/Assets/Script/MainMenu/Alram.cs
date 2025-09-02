using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class Alarm : MonoBehaviour
{
    private Text alarmText;
    public GameObject alarmBG;
    public static Alarm instance = null;

    

    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }


        alarmText = GameObject.Find("alarmText").GetComponent<Text>();
        alarmBG = GameObject.Find("alarmBG");
    }

    private void Start()
    {
        alarmBG.SetActive(false);
    }

    public IEnumerator WriteError(string text)
    {
        yield return new WaitForSeconds(0.2f);
        alarmBG.SetActive(true);
        alarmText.text = text;
        yield return new WaitForSeconds(1.5f);
        alarmText.text = "";
        alarmBG.SetActive(false);
    }
}
