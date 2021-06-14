using System;
using System.Collections.Generic;
using Colyseus;
using LucidSightTools;
using Tanks;
using UnityEngine;

public class TanksColyseusManager : ColyseusManager<TanksColyseusManager>
{
    public delegate void OnRoomsReceived(TanksRoomsAvailable[] rooms);

    public static OnRoomsReceived onRoomsReceived;

    public ColyseusRoom<TanksState> Room
    {
        get
        {
            return  _roomController.Room;
        }
    }

    [SerializeField]
    private TanksRoomController _roomController;

    private bool isInitialized;

    public static bool IsReady
    {
        get
        {
            return Instance != null;
        }
    }

    private string userName;

    /// <summary>
    ///     The display name for the user
    /// </summary>
    public string UserName
    {
        get { return userName; }
        set { userName = value; }
    }

    /// <summary>
    ///     <see cref="MonoBehaviour" /> callback when a script is enabled just before any of the Update methods are called the
    ///     first time.
    /// </summary>
    protected override void Start()
    {
        // For this example we're going to set the target frame rate
        // and allow the app to run in the background for continuous testing.
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        
    }

    public void Initialize(string roomName, Dictionary<string, object> roomOptions)
    {
        if (isInitialized)
        {
            return;
        }

        isInitialized = true;
        // Set up room controller
        _roomController = new TanksRoomController {roomName = roomName};
        _roomController.SetRoomOptions(roomOptions);
    }

    /// <summary>
    /// Initialize the client
    /// </summary>
    public override void InitializeClient()
    {
        base.InitializeClient();

        _roomController.SetClient(client);
    }

    public async void GetAvailableRooms()
    {
        TanksRoomsAvailable[] rooms = await client.GetAvailableRooms<TanksRoomsAvailable>(_roomController.roomName);

        onRoomsReceived?.Invoke(rooms);
    }

    public async void JoinExistingRoom(string roomID, bool isNewJoin)
    {
        await _roomController.JoinRoomId(roomID, isNewJoin);
    }

    public async void CreateNewRoom(string roomID)
    {
        await _roomController.CreateSpecificRoom(client, _roomController.roomName, roomID);
    }

    public async void LeaveAllRooms(Action onLeave)
    {
        await _roomController.LeaveAllRooms(true, onLeave);
    }

    /// <summary>
    ///     On detection of <see cref="OnApplicationQuit" /> will disconnect
    ///     from all <see cref="rooms" />.
    /// </summary>
    private void CleanUpOnAppQuit()
    {
        if (client == null)
        {
            return;
        }

        _roomController.CleanUp();
    }

    /// <summary>
    ///     <see cref="MonoBehaviour" /> callback that gets called just before app exit.
    /// </summary>
    protected override void OnApplicationQuit()
    {
        base.OnApplicationQuit();

        _roomController.LeaveAllRooms(true);

        CleanUpOnAppQuit();
    }

#if UNITY_EDITOR
    public void OnEditorQuit()
    {
        OnApplicationQuit();
    }
#endif

    /// <summary>
    ///     Send an action and message object to the room.
    /// </summary>
    /// <param name="action">The action to take</param>
    /// <param name="message">The message object to pass along to the room</param>
    public static void NetSend(string action, object message = null)
    {
        if (Instance._roomController.Room == null)
        {
            LSLog.LogError($"Error: Not in room for action {action} msg {message}");
            return;
        }

        _ = message == null
            ? Instance._roomController.Room.Send(action)
            : Instance._roomController.Room.Send(action, message);
    }
}