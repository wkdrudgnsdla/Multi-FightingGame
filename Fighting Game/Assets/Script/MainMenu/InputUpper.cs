using UnityEngine;
using UnityEngine.UI;

public class InputUpper : MonoBehaviour
{
    private InputField InputField;

    private void Awake()
    {
        InputField = GameObject.Find("InputCode").GetComponent<InputField>();
    }

    void Start()
    {
        InputField.onValueChanged.AddListener(OnInputValueChanged);
    }

    void OnInputValueChanged(string text)
    {
        InputField.text = text.ToUpper();
    }
}
