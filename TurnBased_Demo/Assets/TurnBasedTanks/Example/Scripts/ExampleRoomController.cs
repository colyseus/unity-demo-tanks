using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Colyseus;
using Colyseus.Schema;
using GameDevWare.Serialization;
using LucidSightTools;
using NativeWebSocket;
using UnityEngine;
using Tanks;
using Vector2 = UnityEngine.Vector2;

/// <summary>
///     Manages the rooms of a server connection.
/// </summary>
[Serializable]
public class ExampleRoomController
{
    // Network Events
    //==========================
    //Custom game delegate functions
    //======================================
    public delegate void OnRoomStateChanged(TanksState state, bool isFirstState);
    public static event OnRoomStateChanged onRoomStateChanged;

    public delegate void OnUserStateChanged(Player state);

    /// <summary>
    ///     The Client that is created when connecting to the Colyseus server.
    /// </summary>
    private ColyseusClient _client;

    public delegate void OnWorldChanged(List<DataChange> changes);
    public static event OnWorldChanged onWorldChanged;

    public delegate void OnWorldGridChanged(string index, float value);
    public static event OnWorldGridChanged onWorldGridChanged;

    public delegate void OnPlayerChange(int playerId, List<DataChange> changes);
    public static event OnPlayerChange onPlayerChange;

    public delegate void OnProjectileAdded(Projectile projectile);
    public static event OnProjectileAdded onProjectileAdded;

    public delegate void OnProjectileRemoved(Projectile cachedProjectile);
    public static event OnProjectileRemoved onProjectileRemoved;

    public delegate void OnProjectileUpdated(Projectile projectile, List<DataChange> changes);
    public static event OnProjectileUpdated onProjectileUpdated;

    public delegate void OnTankMoved(int player, Tanks.Vector2 newCoords);
    public static OnTankMoved onTankMoved;

    //==========================

    /// <summary>
    ///     Used to help calculate the latency of the connection to the server.
    /// </summary>
    private double _lastPing;

    /// <summary>
    ///     Used to help calculate the latency of the connection to the server.
    /// </summary>
    private double _lastPong;

    /// <summary>
    ///     The ID of the room we were just connected to.
    ///     If there is an abnormal disconnect from the current room
    ///     an automatic attempt will be made to reconnect to that room
    ///     with this room ID.
    /// </summary>
    private string _lastRoomId;

    /// <summary>
    ///     Thread responsible for running <see cref="RunPingThread" />
    ///     on a <see cref="ColyseusRoom{T}" />
    /// </summary>
    private Thread _pingThread;

    /// <summary>
    ///     The current or active Room we get when joining or creating a room.
    /// </summary>
    private ColyseusRoom<Tanks.TanksState> _room;

    /// <summary>
    ///     The time as received from the server in milliseconds.
    /// </summary>
    private double _serverTime = -1;

    /// <summary>
    ///     Collection for tracking users that have joined the room.
    /// </summary>
    private IndexedDictionary<int, Player> _users =
        new IndexedDictionary<int, Player>();

    private Dictionary<string, Projectile> _projectiles = new Dictionary<string, Projectile>();

    /// <summary>
    ///     Used to help calculate the latency of the connection to the server.
    /// </summary>
    private bool _waitForPong;

    /// <summary>
    ///     The name of the room clients will attempt to create or join on the Colyseus server.
    /// </summary>
    public string roomName = "NO_ROOM_NAME_PROVIDED";

    private Dictionary<string, object> roomOptionsDictionary = new Dictionary<string, object>();

    /// <summary>
    ///     All the connected rooms.
    /// </summary>
    public List<IColyseusRoom> rooms = new List<IColyseusRoom>();

    /// <summary>
    ///     Returns the synchronized time from the server in milliseconds.
    /// </summary>
    public double GetServerTime
    {
        get { return _serverTime; }
    }

    /// <summary>
    ///     Returns the synchronized time from the server in seconds.
    /// </summary>
    public double GetServerTimeSeconds
    {
        get { return _serverTime / 1000; }
    }

    /// <summary>
    ///     The latency in milliseconds between client and server.
    /// </summary>
    public double GetRoundtripTime
    {
        get { return _lastPong - _lastPing; }
    }

    public ColyseusRoom<Tanks.TanksState> Room
    {
        get { return _room; }
    }

    public string LastRoomID
    {
        get { return _lastRoomId; }
    }

    public void SetRoomOptions(Dictionary<string, object> options)
    {
        roomOptionsDictionary = options;
    }

    /// <summary>
    ///     Set the client of the <see cref="ColyseusRoomManager" />.
    /// </summary>
    /// <param name="client"></param>
    public void SetClient(ColyseusClient client)
    {
        _client = client;
    }

    /// <summary>
    ///     Create a room with the given roomId.
    /// </summary>
    /// <param name="roomId">The ID for the room.</param>
    public async Task CreateSpecificRoom(ColyseusClient client, string roomName, string roomId)
    {
        LSLog.LogImportant($"Creating Room {roomId}");

        try
        {
            //Populate an options dictionary with custom options provided elsewhere as well as the critical option we need here, roomId
            Dictionary<string, object> options = new Dictionary<string, object> {["roomId"] = roomId, ["creatorId"] = ExampleManager.Instance.UserName};
            foreach (KeyValuePair<string, object> option in roomOptionsDictionary)
            {
                options.Add(option.Key, option.Value);
            }

            _room = await client.Create<Tanks.TanksState>(roomName, options);
        }
        catch (Exception ex)
        {
            LSLog.LogError($"Failed to create room {roomId} : {ex.Message}");
            return;
        }

        LSLog.LogImportant($"Created Room: {_room.Id}");
        _lastRoomId = roomId;
        RegisterRoomHandlers();
    }

    /// <summary>
    ///     Join an existing room or create a new one using <see cref="roomName" /> with no options.
    ///     <para>Locked or private rooms are ignored.</para>
    /// </summary>
    public async Task JoinOrCreateRoom(Action<bool> onComplete = null)
    {
        try
        {
            LSLog.LogImportant($"Join Or Create Room - Name = {roomName}.... ");

            // Populate an options dictionary with custom options provided elsewhere
            Dictionary<string, object> options = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> option in roomOptionsDictionary)
            {
                options.Add(option.Key, option.Value);
            }

            _room = await _client.JoinOrCreate<Tanks.TanksState>(roomName, options);
        }
        catch (Exception ex)
        {
            LSLog.LogError($"Room Controller Error - {ex.Message + ex.StackTrace}");

            onComplete?.Invoke(false);

            return;
        }

        onComplete?.Invoke(true);

        LSLog.LogImportant($"Joined / Created Room: {_room.Id}");

        _lastRoomId = _room.Id;
        RegisterRoomHandlers();
    }

    public async Task LeaveAllRooms(bool consented, Action onLeave = null)
    {
        if (_room != null && rooms.Contains(_room) == false)
        {
            await _room.Leave(consented);
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            await rooms[i].Leave(consented);
        }

        _users.Clear();

        ClearRoomHandlers();

        onLeave?.Invoke();
    }

    /// <summary>
    ///     Subscribes the manager to <see cref="room" />'s networked events
    ///     and starts measuring latency to the server.
    /// </summary>
    public virtual void RegisterRoomHandlers()
    {
        if (_pingThread != null)
        {
            _pingThread.Abort();
            _pingThread = null;
        }

        _pingThread = new Thread(RunPingThread);
        _pingThread.Start(_room);

        _room.OnLeave += OnLeaveRoom;

        _room.OnStateChange += OnStateChangeHandler;

        _room.OnMessage<ExamplePongMessage>(0, message =>
        {
            _lastPong = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _serverTime = message.serverTime;
            _waitForPong = false;
        });

        //Custom game logic
        //========================
        _room.State.world.OnChange += OnWorldChange;

        _room.State.world.grid.OnChange += OnWorldGridChange;

        _room.State.players.OnAdd += OnUserAdd;
        _room.State.players.OnRemove += OnUserRemove;

        _room.State.projectiles.OnAdd += OnProjectileAdd;
        _room.State.projectiles.OnRemove += OnProjectileRemove;
        //========================

        _room.State.TriggerAll();

        _room.colyseusConnection.OnError += Room_OnError;
        _room.colyseusConnection.OnClose += Room_OnClose;
    }

    private void OnWorldChange(List<DataChange> changes)
    {
        onWorldChanged?.Invoke(changes);
    }

    private void OnWorldGridChange(string index, float value)
    {
        onWorldGridChanged?.Invoke(index, value);
    }

    private void OnLeaveRoom(WebSocketCloseCode code)
    {
        LSLog.Log("ROOM: ON LEAVE =- Reason: " + code);
        _pingThread.Abort();
        _pingThread = null;
        _room = null;

        if (code != WebSocketCloseCode.Normal && !string.IsNullOrEmpty(_lastRoomId))
        {
            JoinRoomId(_lastRoomId, false);
        }
    }

    /// <summary>
    ///     Unsubscribes <see cref="Room" /> from networked events."/>
    /// </summary>
    private void ClearRoomHandlers()
    {
        if (_pingThread != null)
        {
            _pingThread.Abort();
            _pingThread = null;
        }

        if (_room == null)
        {
            return;
        }

        _room.OnStateChange -= OnStateChangeHandler;

        _room.State.world.OnChange -= OnWorldChange;
        _room.State.world.grid.OnChange -= OnWorldGridChange;
        _room.State.projectiles.OnAdd -= OnProjectileAdd;
        _room.State.projectiles.OnRemove -= OnProjectileRemove;
        _room.State.players.OnAdd -= OnUserAdd;
        _room.State.players.OnRemove -= OnUserRemove;

        _room.colyseusConnection.OnError -= Room_OnError;
        _room.colyseusConnection.OnClose -= Room_OnClose;

        _room.OnLeave -= OnLeaveRoom;

        _room = null;
    }

    /// <summary>
    ///     Asynchronously gets all the available rooms of the <see cref="_client" />
    ///     named <see cref="roomName" />
    /// </summary>
    public async Task<ColyseusRoomAvailable[]> GetRoomListAsync()
    {
        ColyseusRoomAvailable[] allRooms = await _client.GetAvailableRooms(roomName);

        return allRooms;
    }

    /// <summary>
    ///     Join a room with the given <see cref="roomId" />.
    /// </summary>
    /// <param name="roomId">ID of the room to join.</param>
    public async Task JoinRoomId(string roomId, bool isNewJoin)
    {
        ClearRoomHandlers();

        try
        {
            while (_room == null || !_room.colyseusConnection.IsOpen)
            {
                Dictionary<string, object> options = new Dictionary<string, object>();

                options.Add("joiningId", ExampleManager.Instance.UserName);

                _room = await _client.JoinById<Tanks.TanksState>(roomId, options);

                if (_room == null || !_room.colyseusConnection.IsOpen)
                {
                    LSLog.LogImportant($"Failed to Connect to {roomId}.. Retrying in 5 Seconds...");
                    await Task.Delay(5000);
                }
            }
            LSLog.LogImportant($"Connected to {roomId}..");
            _lastRoomId = roomId;
            RegisterRoomHandlers();
        }
        catch (Exception ex)
        {
            LSLog.LogError(ex.Message);
            LSLog.LogError("Failed to join room");
        }
    }

    /// <summary>
    ///     Callback for when a <see cref="ExampleNetworkedUser" /> is added to a room.
    /// </summary>
    /// <param name="user">The user object</param>
    /// <param name="key">The user key</param>
    private void OnUserAdd(int key, Tanks.Player user)
    {
        // Add "player" to map of players
        _users.Add(key, user);

        user.coords.OnChange += coordChanges =>
        {
            onTankMoved?.Invoke((int)user.playerId, user.coords);
        };

        // On entity update...
        user.OnChange += changes =>
        {
            onPlayerChange?.Invoke((int)user.playerId, changes);
        };
    }

    private void OnProjectileAdd(string key, Projectile projectile)
    {
        //LSLog.LogImportant($"Projectile Added! - Key = {key}");

        _projectiles.Add(key, projectile);

        projectile.coords.OnChange += (changes) => { onProjectileUpdated?.Invoke(projectile, changes); };

        projectile.OnChange += (changes) => { onProjectileUpdated?.Invoke(projectile, changes); };

        onProjectileAdded?.Invoke(projectile);
    }

    private void OnProjectileRemove(string key, Projectile projectile)
    {
        //LSLog.LogImportant($"Projectile Removed! - Key = {key}");

        if (_projectiles.ContainsKey(key))
        {
            onProjectileRemoved?.Invoke(_projectiles[key]);

            _projectiles.Remove(key);
        }
        else
        {
            LSLog.LogError($"No projectile for key - {key}");
        }
    }

    /// <summary>
    ///     Callback for when a user is removed from a room.
    /// </summary>
    /// <param name="user">The removed user.</param>
    /// <param name="key">The user key.</param>
    private void OnUserRemove(int key, Player/*ExampleNetworkedUser*/ user)
    {
        LSLog.LogImportant($"user [{user.__refId} | {user.sessionId/*id*/} | key {key}] Left");

        _users.Remove(key);
    }

    /// <summary>
    ///     Callback for when the room's connection closes.
    /// </summary>
    /// <param name="closeCode">Code reason for the connection close.</param>
    private static void Room_OnClose(WebSocketCloseCode closeCode)
    {
        LSLog.LogError("Room_OnClose: " + closeCode);
    }

    /// <summary>
    ///     Callback for when the room get an error.
    /// </summary>
    /// <param name="errorMsg">The error message.</param>
    private static void Room_OnError(string errorMsg)
    {
        LSLog.LogError("Room_OnError: " + errorMsg);
    }

    /// <summary>
    ///     Callback when the room state has changed.
    /// </summary>
    /// <param name="state">The room state.</param>
    /// <param name="isFirstState">Is it the first state?</param>
    private static void OnStateChangeHandler(Tanks.TanksState state, bool isFirstState)
    {
        // Setup room first state
        onRoomStateChanged?.Invoke(state, isFirstState);
    }

    /// <summary>
    ///     Sends "ping" message to current room to help measure latency to the server.
    /// </summary>
    /// <param name="roomToPing">The <see cref="ColyseusRoom{T}" /> to ping.</param>
    private void RunPingThread(object roomToPing)
    {
        ColyseusRoom<TanksState> currentRoom = (ColyseusRoom<TanksState>) roomToPing;

        const float pingInterval = 0.5f; // seconds
        const float pingTimeout = 15f; //seconds

        int timeoutMilliseconds = Mathf.FloorToInt(pingTimeout * 1000);
        int intervalMilliseconds = Mathf.FloorToInt(pingInterval * 1000);

        DateTime pingStart;
        while (currentRoom != null)
        {
            _waitForPong = true;
            pingStart = DateTime.Now;
            _lastPing = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _ = currentRoom.Send("ping");

            while (currentRoom != null && _waitForPong &&
                   DateTime.Now.Subtract(pingStart).TotalSeconds < timeoutMilliseconds)
            {
                Thread.Sleep(200);
            }

            if (_waitForPong)
            {
                LSLog.LogError("Ping Timed out");
            }

            Thread.Sleep(intervalMilliseconds);
        }
    }

    /// <summary>
    ///     Increments the known <see cref="_serverTime" /> by <see cref="Time.fixedDeltaTime" />
    ///     converted into milliseconds.
    /// </summary>
    public void IncrementServerTime()
    {
        _serverTime += Time.fixedDeltaTime * 1000;
    }

    public async void CleanUp()
    {
        _pingThread?.Abort();

        List<Task> leaveRoomTasks = new List<Task>();

        foreach (IColyseusRoom roomEl in rooms)
        {
            leaveRoomTasks.Add(roomEl.Leave(false));
        }

        if (_room != null)
        {
            leaveRoomTasks.Add(_room.Leave(false));
        }

        await Task.WhenAll(leaveRoomTasks.ToArray());
    }
}