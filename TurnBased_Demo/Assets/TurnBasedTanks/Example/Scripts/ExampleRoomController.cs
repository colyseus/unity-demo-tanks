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
    public delegate void OnBeginRound();

    public delegate void OnBeginRoundCountDown();

    // Network Events
    //==========================
    /// <summary>
    ///     OnNetworkEntityAdd delegate for OnNetworkEntityAdd event.
    /// </summary>
    /// <param name="entity">Then entity that was just added to the room.</param>
    public delegate void OnNetworkEntityAdd(ExampleNetworkedEntity entity);

    /// <summary>
    ///     OnNetworkEntityRemoved delegate for OnNetworkEntityRemoved event.
    /// </summary>
    /// <param name="entity">Then entity that was just removed to the room.</param>
    public delegate void OnNetworkEntityRemoved(ExampleNetworkedEntity entity, ColyseusNetworkedEntityView view);

    //Custom game delegate functions
    //======================================
    public delegate void OnRoomStateChanged(TanksState state, bool isFirstState);


    public delegate void OnUserStateChanged(Player state/*MapSchema<string> changes*/);

    /// <summary>
    ///     Event for when a NetworkEntity is added to the room.
    /// </summary>
    public static OnNetworkEntityAdd onAddNetworkEntity;

    /// <summary>
    ///     Event for when a NetworkEntity is added to the room.
    /// </summary>
    public static OnNetworkEntityRemoved onRemoveNetworkEntity;

    /// <summary>
    ///     Our user object we get upon joining a room.
    /// </summary>
    [SerializeField]
    private static ExampleNetworkedUser _currentNetworkedUser;

    /// <summary>
    ///     The Client that is created when connecting to the Colyseus server.
    /// </summary>
    private ColyseusClient _client;

    private ColyseusSettings _colyseusSettings;

    /// <summary>
    ///     Collection of entity creation callbacks. Callbacks are added to
    ///     the collection when a <see cref="ExampleNetworkedEntity" /> is created.
    ///     The callbacks are invoked and removed from the collection once the
    ///     entity has been added to the room.
    /// </summary>
    private Dictionary<string, Action<ExampleNetworkedEntity>> _creationCallbacks =
        new Dictionary<string, Action<ExampleNetworkedEntity>>();


    //public delegate void OnInitialSetup(int playerTurnId, int currentPlayerTurn, int currentAP, string[] playerNames, int[] playerHP, WeaponModel currentWeapon, List<List<int>> mapMatrix, bool challengerOnline);
    //public static event OnInitialSetup onInitialSetup;

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

    public delegate void OnReceivedFirePath(int player, int remainingAP, List<Vector3> firePath, DamageData damageData);
    public static event OnReceivedFirePath onReceivedFirePath;

    public delegate void OnSelectedWeaponUpdated(WeaponModel selectedWeapon);
    public static event OnSelectedWeaponUpdated onSelectedWeaponUpdated;

    public delegate void OnPlayerJoined(int playerId, string playerName);
    public static event OnPlayerJoined onPlayerJoined;

    public delegate void OnPlayerQuit(string playerName);
    public static event OnPlayerQuit onPlayerQuit;

    public delegate void OnPlayerLeave();
    public static event OnPlayerLeave onPlayerLeave;

    public delegate void OnTurnCompleted(bool wasSkip);
    public static OnTurnCompleted onTurnCompleted;

    public delegate void OnTankMoved(int player, Tanks.Vector2 newCoords);
    public static OnTankMoved onTankMoved;

    //==========================

    // TODO: Replace GameDevWare stuff
    /// <summary>
    ///     Collection for tracking entities that have been added to the room.
    /// </summary>
    private IndexedDictionary<string, ExampleNetworkedEntity> _entities =
        new IndexedDictionary<string, ExampleNetworkedEntity>();

    /// <summary>
    ///     Collection for tracking entity views that have been added to the room.
    /// </summary>
    private IndexedDictionary<string, ExampleNetworkedEntityView> _entityViews =
        new IndexedDictionary<string, ExampleNetworkedEntityView>();

    private ExampleNetworkedEntityFactory _factory;

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

    public IndexedDictionary<string, ExampleNetworkedEntity> Entities
    {
        get { return _entities; }
    }

    public IndexedDictionary<string, ExampleNetworkedEntityView> EntityViews
    {
        get { return _entityViews; }
    }

    public Dictionary<string, Action<ExampleNetworkedEntity>> CreationCallbacks
    {
        get { return _creationCallbacks; }
    }

    public ExampleNetworkedUser CurrentNetworkedUser
    {
        get { return _currentNetworkedUser; }
    }

    /// <summary>
    ///     Checks if a <see cref="ExampleNetworkedEntityView" /> exists for
    ///     the given ID.
    /// </summary>
    /// <param name="entityId">The ID of the <see cref="ExampleNetworkedEntity" /> we're checking for.</param>
    /// <returns></returns>
    public bool HasEntityView(string entityId)
    {
        return EntityViews.ContainsKey(entityId);
    }

    /// <summary>
    ///     Returns a <see cref="ExampleNetworkedEntityView" /> given <see cref="entityId" />
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns>
    ///     Returns <see cref="ExampleNetworkedEntityView" /> if one exists for the given <see cref="entityId" />
    /// </returns>
    public ExampleNetworkedEntityView GetEntityView(string entityId)
    {
        if (EntityViews.ContainsKey(entityId))
        {
            return EntityViews[entityId];
        }

        return null;
    }

    public static event OnRoomStateChanged onRoomStateChanged;
    public static event OnBeginRoundCountDown onBeginRoundCountDown;
    public static event OnBeginRound onBeginRound;
    //public static event OnUserStateChanged OnCurrentUserStateChanged;

    /// <summary>
    ///     Set the dependencies.
    /// </summary>
    /// <param name="roomName"></param>
    /// <param name="settings"></param>
    public void SetDependencies(ColyseusSettings settings)
    {
        _colyseusSettings = settings;

        ColyseusClient.onAddRoom += AddRoom;
    }

    public void SetRoomOptions(Dictionary<string, object> options)
    {
        roomOptionsDictionary = options;
    }

    /// <summary>
    ///     Set the <see cref="NetworkedEntitExampleNetworkedEntityFactoryyFactory" /> of the RoomManager.
    /// </summary>
    /// <param name="factory"></param>
    public void SetNetworkedEntityFactory(ExampleNetworkedEntityFactory factory)
    {
        _factory = factory;
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
    ///     Adds the given room to <see cref="rooms" />
    /// </summary>
    /// <param name="roomToAdd"></param>
    /// <returns></returns>
    public void AddRoom(IColyseusRoom roomToAdd)
    {
        roomToAdd.OnLeave += code => rooms.Remove(roomToAdd);
        rooms.Add(roomToAdd);
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

        _entities.Clear();
        _entityViews.Clear();
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
        LSLog.LogImportant($"sessionId: {_room.SessionId}");

        if (_pingThread != null)
        {
            _pingThread.Abort();
            _pingThread = null;
        }

        _pingThread = new Thread(RunPingThread);
        _pingThread.Start(_room);

        _room.OnLeave += OnLeaveRoom;

        _room.OnStateChange += OnStateChangeHandler;

        _room.OnMessage<ExampleNetworkedUser>("onJoin", currentNetworkedUser =>
        {
            _currentNetworkedUser = currentNetworkedUser;
        });

        _room.OnMessage<ExampleRFCMessage>("onRFC", _rfc =>
        {
            if (_entityViews.Keys.Contains(_rfc.entityId))
            {
                _entityViews[_rfc.entityId].RemoteFunctionCallHandler(_rfc);
            }
        });

        _room.OnMessage<ExamplePongMessage>(0, message =>
        {
            _lastPong = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _serverTime = message.serverTime;
            _waitForPong = false;
        });

        //Custom game logic
        _room.State.world.OnChange += changes => onWorldChanged?.Invoke(changes);

        _room.State.world.grid.OnChange += (index, value) => onWorldGridChanged?.Invoke(index, value);

        _room.OnMessage<TankTurnUpdateMessage>("turnComplete", (message) =>
        {
            onTurnCompleted?.Invoke(message.wasSkip);
        });
        //_room.OnMessage<TankMoveMessage>("tankMoved", (message) =>
        //{
        //    onTankMoved?.Invoke(message.playerNumber, message.remainingAP, message.newCoords);
        //});
        _room.OnMessage<FirePathMessage>("receiveFirePath", (message) => { onReceivedFirePath?.Invoke(message.playerNumber, message.remainingAP, message.firePath, message.damageData ); });

        //_room.OnMessage<SelectedWeaponUpdatedMessage>("selectedWeaponUpdated", (message) => { onSelectedWeaponUpdated?.Invoke(message.weapon);});

        _room.OnMessage<PlayerJoinedMessage>("playerJoined", (message) => { onPlayerJoined?.Invoke(message.playerId, message.playerName); });
        
        _room.OnMessage<PlayerQuitMessage>("onPlayerQuitGame", (message) => { onPlayerQuit?.Invoke(message.playerName); });

        _room.OnMessage<ExampleCustomMethodMessage>("onPlayerLeave", (message) => {onPlayerLeave?.Invoke();});
        //========================
        
        _room.State.players.OnAdd += OnUserAdd;
        _room.State.players.OnRemove += OnUserRemove;

        _room.State.projectiles.OnAdd += OnProjectileAdd;
        _room.State.projectiles.OnRemove += OnProjectileRemove;

        _room.State.TriggerAll();
        //========================

        _room.colyseusConnection.OnError += Room_OnError;
        _room.colyseusConnection.OnClose += Room_OnClose;
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

        //_room.State.networkedEntities.OnAdd -= OnEntityAdd;
        //_room.State.networkedEntities.OnRemove -= OnEntityRemoved;
        _room.State.players/*networkedUsers*/.OnAdd -= OnUserAdd;
        _room.State.players/*networkedUsers*/.OnRemove -= OnUserRemove;

        _room.colyseusConnection.OnError -= Room_OnError;
        _room.colyseusConnection.OnClose -= Room_OnClose;

        _room.OnStateChange -= OnStateChangeHandler;

        _room.OnLeave -= OnLeaveRoom;

        _room = null;
        _currentNetworkedUser = null;
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
        LSLog.Log($"Joining Room ID {roomId}....");
        ClearRoomHandlers();

        try
        {
            while (_room == null || !_room.colyseusConnection.IsOpen)
            {
                Dictionary<string, object> options = new Dictionary<string, object>();
                //if (isNewJoin)
                //{
                    options.Add("joiningId", ExampleManager.Instance.UserName);
                //}
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
            //await CreateSpecificRoom(_client, roomName, roomId, onJoin);
        }
    }

    /// <summary>
    ///     The callback for the event when a <see cref="ExampleNetworkedEntity" /> is added to a room.
    /// </summary>
    /// <param name="entity">The entity that was just added.</param>
    /// <param name="key">The entity's key</param>
    private async void OnEntityAdd(ExampleNetworkedEntity entity, string key)
    {
        Debug.Log(
            $"Entity [{entity.__refId} | {entity.id}] add: x => {entity.xPos}, y => {entity.yPos}, z => {entity.zPos}");

        _entities.Add(entity.id, entity);

        //Creation ID is only Registered with the owner so only owners callback will be triggered
        if (!string.IsNullOrEmpty(entity.creationId) && _creationCallbacks.ContainsKey(entity.creationId))
        {
            _creationCallbacks[entity.creationId].Invoke(entity);
            _creationCallbacks.Remove(entity.creationId);
        }
        else
        {
            Debug.Log($"***Not triggering callback for entity creation! ID: {entity.creationId}, Contains key? {_creationCallbacks.ContainsKey(entity.creationId)} ***");
        }

        onAddNetworkEntity?.Invoke(entity);

        if (_entityViews.ContainsKey(entity.id) == false && !string.IsNullOrEmpty(entity.attributes["prefab"]))
        {
            await _factory.CreateFromPrefab(entity);
        }
        else
        {
            Debug.Log($"***Not Creating prefab for entity for entity creation! ID: {entity.id}, Contains key? {_entityViews.ContainsKey(entity.id)} ***");
        }
    }

    /// <summary>
    ///     The callback for the event when a <see cref="ExampleNetworkedEntity" /> is removed from a room.
    /// </summary>
    /// <param name="entity">The entity that was just removed.</param>
    /// <param name="key">The entity's key</param>
    private void OnEntityRemoved(ExampleNetworkedEntity entity, string key)
    {
        if (_entities.ContainsKey(entity.id))
        {
            _entities.Remove(entity.id);
        }

        ColyseusNetworkedEntityView view = null;

        if (_entityViews.ContainsKey(entity.id))
        {
            view = _entityViews[entity.id];
            _entityViews.Remove(entity.id);
        }

        onRemoveNetworkEntity?.Invoke(entity, view);
    }

    /// <summary>
    ///     Callback for when a <see cref="ExampleNetworkedUser" /> is added to a room.
    /// </summary>
    /// <param name="user">The user object</param>
    /// <param name="key">The user key</param>
    private void OnUserAdd(int key, Tanks.Player user)
    {
        //LSLog.LogImportant($"user [{user.__refId} | {user.sessionId} | key {key}] Joined");

        // Add "player" to map of players
        _users.Add(key, user);

        user.coords.OnChange += coordChanges =>
        {
            onTankMoved?.Invoke((int)user.playerId, user.coords);
        };

        // On entity update...
        user.OnChange += changes =>
        {
            //LSLog.LogImportant($"User.OnChange!", LSLog.LogColor.cyan);
            //user.updateHash = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            //// If the change is for our current user then fire the event with the attributes that changed
            //if (ExampleManager.Instance.CurrentUser != null &&
            //    string.Equals(ExampleManager.Instance.CurrentUser.sessionId, user.sessionId))
            //{
            //    OnCurrentUserStateChanged?.Invoke(user/*.attributes*/);
            //}
            
            //LSLog.LogImportant($"Player Change - Player = {user.sessionId}/{user.playerId}/{user.name}", LSLog.LogColor.grey);

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