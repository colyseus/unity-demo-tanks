using System;
using System.Collections;
using System.Collections.Generic;
using Colyseus;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyController : MonoBehaviour
{
    [SerializeField]
    private GameObject connectingCover = null;

    [SerializeField]
    private CreateUserMenu createUserMenu = null;

    //Variables to initialize the room controller
    public string roomName = "YOURROOM";
    public string nextSceneName = "GAMESCENE";

    [SerializeField]
    private RoomSelectionMenu selectRoomMenu = null;

    private void Awake()
    {
        createUserMenu.gameObject.SetActive(true);
        selectRoomMenu.gameObject.SetActive(false);
        connectingCover.SetActive(true);
    }

    private IEnumerator Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        while (!TanksColyseusManager.IsReady)
        {
            yield return new WaitForEndOfFrame();
        }

        Dictionary<string, object> roomOptions = new Dictionary<string, object>();

        TanksColyseusManager.Instance.Initialize(roomName, roomOptions);
        TanksColyseusManager.onRoomsReceived += OnRoomsReceived;
        connectingCover.SetActive(false);
    }

    private void OnDestroy()
    {
        TanksColyseusManager.onRoomsReceived -= OnRoomsReceived;
    }

    /// <summary>
    /// Used with button input from the user to continue with the desired username
    /// </summary>
    public void CreateUser()
    {
        string desiredUserName = createUserMenu.UserName;
        PlayerPrefs.SetString("UserName", desiredUserName);

        ColyseusSettings clonedSettings = TanksColyseusManager.Instance.CloneSettings();
        clonedSettings.colyseusServerAddress = createUserMenu.ServerURL;
        clonedSettings.colyseusServerPort = createUserMenu.ServerPort;
        clonedSettings.useSecureProtocol = createUserMenu.UseSecure;

        TanksColyseusManager.Instance.OverrideSettings(clonedSettings);

        TanksColyseusManager.Instance.InitializeClient();

        TanksColyseusManager.Instance.UserName = desiredUserName;
        //Do user creation stuff
        createUserMenu.gameObject.SetActive(false);
        selectRoomMenu.gameObject.SetActive(true);
        selectRoomMenu.GetAvailableRooms();
    }

    public void CreateRoom()
    {
        connectingCover.SetActive(true);
        string desiredRoomName = selectRoomMenu.RoomCreationName;
        LoadNextScene(() => { TanksColyseusManager.Instance.CreateNewRoom(desiredRoomName); });
    }

    public void JoinRoom(string id)
    {
        connectingCover.SetActive(true);
        LoadNextScene(() => { TanksColyseusManager.Instance.JoinExistingRoom(id, true); });
    }

    public void ReJoinRoom(string id)
    {
        connectingCover.SetActive(true);
        LoadNextScene(() => { TanksColyseusManager.Instance.JoinExistingRoom(id, false); });
    }

    public void OnConnectedToServer()
    {
        connectingCover.SetActive(false);
    }

    private void OnRoomsReceived(TanksRoomsAvailable[] rooms)
    {
        selectRoomMenu.HandRooms(TrimRooms(rooms));
    }

    private TanksRoomsAvailable[] TrimRooms(TanksRoomsAvailable[] originalRooms)
    {
        List<TanksRoomsAvailable> trimmedRooms = new List<TanksRoomsAvailable>();
        for (int i = 0; i < originalRooms.Length; ++i)
        {
            //Check a rooms metadata. If its one of our rooms OR waiting for a player, we show it
            TanksRoomMetadata metadata = originalRooms[i].metadata;
            if (metadata.team1 == null || (metadata.team1.Equals(TanksColyseusManager.Instance.UserName) ||
                                           metadata.team0.Equals(TanksColyseusManager.Instance.UserName)))
            {
                trimmedRooms.Add(originalRooms[i]);
            }
        }

        return trimmedRooms.ToArray();
    }

    private void LoadNextScene(Action onComplete)
    {
        StartCoroutine(LoadSceneAsync(nextSceneName, onComplete));
    }

    private IEnumerator LoadSceneAsync(string scene, Action onComplete)
    {
        Scene currScene = SceneManager.GetActiveScene();
        AsyncOperation op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
        while (op.progress <= 0.9f)
        {
            //Wait until the scene is loaded
            yield return new WaitForEndOfFrame();
        }

        onComplete.Invoke();
        op.allowSceneActivation = true;
        SceneManager.UnloadSceneAsync(currScene);
    }
}