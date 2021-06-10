using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateUserMenu : MonoBehaviour
{
    [SerializeField]
    private Button createButton = null;

    [SerializeField]
    private TMP_InputField inputField = null;

    [SerializeField]
    private TMP_InputField serverURLInput = null;
    [SerializeField]
    private TMP_InputField serverPortInput = null;
    [SerializeField]
    private Toggle secureToggle;

    public string UserName
    {
        get { return inputField.text; }
    }

    public string ServerURL
    {
        get
        {
            if (string.IsNullOrEmpty(serverURLInput.text) == false)
            {
                return serverURLInput.text;
            }

            return TanksColyseusManager.Instance.ColyseusServerAddress;
        }
    }

    public string ServerPort
    {
        get
        {
            if (string.IsNullOrEmpty(serverPortInput.text) == false)
            {
                return serverPortInput.text;
            }

            return TanksColyseusManager.Instance.ColyseusServerPort;
        }
    }

    public bool UseSecure
    {
        get
        {
            return secureToggle.isOn;
        }
    }

    private void Awake()
    {
        createButton.interactable = false;
        string oldName = PlayerPrefs.GetString("UserName", "");
        if (oldName.Length > 0)
        {
            inputField.text = oldName;
            createButton.interactable = true;
        }

    }

    private void Start()
    {
        serverURLInput.text = TanksColyseusManager.Instance.ColyseusServerAddress;
        serverPortInput.text = TanksColyseusManager.Instance.ColyseusServerPort;
        secureToggle.isOn = TanksColyseusManager.Instance.ColyseusUseSecure;
    }

    public void OnInputFieldChange()
    {
        createButton.interactable = inputField.text.Length > 0;
    }
}