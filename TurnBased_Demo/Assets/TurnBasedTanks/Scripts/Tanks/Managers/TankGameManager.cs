using System;
using System.Collections;
using System.Collections.Generic;
using Colyseus.Schema;
using LucidSightTools;
using Tanks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vector2 = UnityEngine.Vector2;

public class TankGameManager : MonoBehaviour
{
    public enum eTurn
    {
        PLAYER_1 = 0,
        PLAYER_2
    }

    public eTurn CurrentTurn { get; private set; }

    public EnvironmentBuilder Builder => environmentBuilder;

    [SerializeField]
    private EnvironmentBuilder environmentBuilder = null;

    [SerializeField]
    private GameUIController uiController;

    [SerializeField]
    private CameraManager cameraManager = null;

    public ProjectileBase projectilePrefab;

    private TankController playerOneTank;
    private TankController playerTwoTank;

    private bool fireChargeInProgress = false;
    public static TankGameManager Instance { get; private set; }

    public int OurPlayerID { get; private set; } = -1;
    public string OurPlayerName { get; private set; }
    public string EnemyName { get; private set; }
    public string GeneralMessage { get; private set; }
    public bool IsGameOver { get; private set; } = false;

    private Dictionary<string, string> attributeUpdate = new Dictionary<string, string>();

    private Dictionary<Projectile, ProjectileBase> _projectileObjects = new Dictionary<Projectile, ProjectileBase>();

    private bool _waitingForFirePath = false;

    public bool IsOurTurn
    {
        get
        {
            return ((eTurn) OurPlayerID) == CurrentTurn;
        }
    }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    IEnumerator Start()
    {
        yield return new WaitForFixedUpdate();
    }

    void OnEnable()
    {
        // Subscribe to events
        ExampleRoomController.onRoomStateChanged += OnRoomStateChanged;
        //ExampleRoomController.onInitialSetup += OnInitialSetup;
        ExampleRoomController.onTankMoved += OnPlayerMove;
        ExampleRoomController.onReceivedFirePath += OnReceivedFirePath;
        //ExampleRoomController.onSelectedWeaponUpdated += OnSelectedWeaponUpdated;
        ExampleRoomController.onTurnCompleted += OnTurnCompleted;
        ExampleRoomController.onPlayerJoined += OnPlayerJoined;
        ExampleRoomController.onPlayerQuit += OnPlayerQuit;
        ExampleRoomController.onPlayerLeave += OnPlayerLeft;

        ExampleRoomController.onWorldChanged += OnWorldChanged;
        ExampleRoomController.onPlayerChange += OnPlayerUpdated;
        ExampleRoomController.onProjectileAdded += OnProjectileAdded;
        ExampleRoomController.onProjectileRemoved += OnProjectileRemoved;

        ExampleRoomController.onProjectileUpdated += OnProjectileUpdated;
    }

    void OnDisable()
    {
        // Unsubscribe from events
        ExampleRoomController.onRoomStateChanged -= OnRoomStateChanged;
        //ExampleRoomController.onInitialSetup -= OnInitialSetup;
        ExampleRoomController.onTankMoved -= OnPlayerMove;
        ExampleRoomController.onReceivedFirePath -= OnReceivedFirePath;
        //ExampleRoomController.onSelectedWeaponUpdated -= OnSelectedWeaponUpdated;
        ExampleRoomController.onTurnCompleted -= OnTurnCompleted;
        ExampleRoomController.onPlayerJoined -= OnPlayerJoined;
        ExampleRoomController.onPlayerQuit -= OnPlayerQuit;
        ExampleRoomController.onPlayerLeave -= OnPlayerLeft;

        ExampleRoomController.onWorldChanged -= OnWorldChanged;
        ExampleRoomController.onPlayerChange -= OnPlayerUpdated;
        ExampleRoomController.onProjectileAdded -= OnProjectileAdded;
        ExampleRoomController.onProjectileRemoved -= OnProjectileRemoved;

        ExampleRoomController.onProjectileUpdated -= OnProjectileUpdated;
    }

    private void OnProjectileAdded(Projectile projectile)
    {
        if (_projectileObjects.ContainsKey(projectile) == false)
        {
            // Create new projectile object
            ProjectileBase proj = Instantiate(projectilePrefab);
            proj.transform.SetParent(Builder.groundPieceRoot);
            proj.transform.localPosition = new Vector3(projectile.coords.x, projectile.coords.y);

            _projectileObjects.Add(projectile, proj);
        }
        else
        {
            LSLog.LogError($"On Projectile Added - Projectile has already been added!");
        }
    }

    private void OnProjectileRemoved(Projectile projectile)
    {
        if (_projectileObjects.ContainsKey(projectile))
        {
            // Create new projectile object
            ProjectileBase proj = _projectileObjects[projectile];

            proj.Explode();

            Destroy(proj.gameObject);

            _projectileObjects.Remove(projectile);
        }
        else
        {
            LSLog.LogError($"On Projectile Added - No projectile object for projectile!");
        }
    }

    private void OnProjectileUpdated(Projectile projectile, List<DataChange> changes)
    {
        if (_projectileObjects.ContainsKey(projectile))
        {
            _projectileObjects[projectile].UpdateTargetPosition(projectile.coords);
        }
        else
        {
            LSLog.LogError($"On Projectile Update - No projectile object for projectile!");
        }
    }

    private void OnPlayerUpdated(int playerId, List<DataChange> changes)
    {
        LSLog.LogImportant($"Player Updated - Player Id = {playerId}", LSLog.LogColor.grey);

        for (int i = 0; i < changes.Count; i++)
        {
            LSLog.LogImportant($"\tField = {changes[i].Field} Prev = {changes[i].PreviousValue}  New = {changes[i].Value}", LSLog.LogColor.grey);
            
            UpdatePlayer(playerId, changes[i]);
        }
    }

    private void UpdatePlayer(int playerId, DataChange change)
    {
        TankController tank = playerId == 0 ? playerOneTank : playerTwoTank;
        TanksState state = ExampleManager.Instance.Room.State;

        switch (change.Field)
        {
            case "currentWeapon":
                Weapon weapon = (Weapon)state.weapons[(int)((float)change.Value)];
                tank.UpdateSelectedWeapon(weapon);
                break;
            case "aimAngle":
                if (playerId != OurPlayerID)
                {
                    tank.SetAim((float)change.Value);
                }
                break;
            case "connected":
                if (playerId != OurPlayerID)
                {
                    uiController.ToggleOnlineIndicator((bool)change.Value);
                }
                break;
            case "name":

                string updatedName = (string)change.Value;

                if (OurPlayerID == playerId)
                {
                    OurPlayerName = updatedName;
                }
                else
                {
                    EnemyName = updatedName;
                }

                uiController.UpdatePlayerNames(OurPlayerName, EnemyName);

                break;
            default:
                LSLog.Log($"Unsupported update field - \"{change.Field}\"", LSLog.LogColor.yellow);

                break;
        }

        UpdateUI(state);
    }

    /// <summary>
    /// Callback to handle another player joining while we are connected to the room
    /// </summary>
    /// <param name="playerId"></param>
    /// <param name="playerName"></param>
    private void OnPlayerJoined(int playerId, string playerName)
    {
        if (playerId == OurPlayerID)
        {
            return;
        }

        EnemyName = playerName;

        uiController.UpdatePlayerNames(OurPlayerName, playerName);
        uiController.ToggleOnlineIndicator(true);

        UpdateUI(ExampleManager.Instance.Room.State);
    }

    /// <summary>
    /// Callback to handle the other player quitting while we are connected to the room
    /// </summary>
    /// <param name="quittingPlayerName"></param>
    private void OnPlayerQuit(string quittingPlayerName)
    {
        string quitMsg = IsGameOver ? $"{quittingPlayerName} has left" : $"{quittingPlayerName} Surrendered!";

        uiController.ShowGameOverUI(OurPlayerID, quitMsg);
        
        IsGameOver = true;
    }

    /// <summary>
    /// Callback to handle when a player has left the room
    /// </summary>
    private void OnPlayerLeft()
    {
        uiController.ToggleOnlineIndicator(false);
    }

    private void SelectedWeaponUpdated(int playerId, int weaponIndex = 0)
    {
        Weapon weapon = ExampleManager.Instance.Room.State.weapons[weaponIndex];

        if (OurPlayerID == 0)
        {
            playerOneTank.UpdateSelectedWeapon(weapon);
        }
        else
        {
            playerTwoTank.UpdateSelectedWeapon(weapon);
        }

        UpdateUI(ExampleManager.Instance.Room.State);
    }

    /// <summary>
    /// Callback to handle the server response when we want to change our currently selected weapon
    /// </summary>
    /// <param name="selectedWeapon"></param>
    //private void OnSelectedWeaponUpdated(WeaponModel selectedWeapon)
    //{
    //    if (OurPlayerID == 0)
    //    {
    //        playerOneTank.UpdateSelectedWeapon(selectedWeapon);
    //    }
    //    else
    //    {
    //        playerTwoTank.UpdateSelectedWeapon(selectedWeapon);
    //    }

    //    UpdateUI(ExampleManager.Instance.Room.State);
    //}

    /// <summary>
    /// Callback to handle an inbound fire path
    /// </summary>
    /// <param name="player">The turn Id of the player that fired/requested the fire path</param>
    /// <param name="remainingAP">The remaining Action Points of the player</param>
    /// <param name="firePath">The path of the projectile</param>
    /// <param name="damageData">DamageData result at the end of the fire path</param>
    private void OnReceivedFirePath(int player, int remainingAP, List<Vector3> firePath, DamageData damageData)
    {
        _waitingForFirePath = false;
        CancelInvoke("ResetWaitForFirePath");

        TankController tank = player == 0 ? playerOneTank : playerTwoTank;
        tank.CurrentAP = remainingAP;

        UpdateUI(ExampleManager.Instance.Room.State);

        CannonController.CannonFirePath cannonFirePath = new CannonController.CannonFirePath(firePath.ToArray());

        GetTankForCurrentTurn().Fire(cannonFirePath, damageData);
    }

    /// <summary>
    /// Callback for the room state change
    /// </summary>
    /// <param name="attributes"></param>
    private void OnRoomStateChanged(TanksState state, bool isFirstState)
    {
        //LSLog.LogImportant($"On Room State Changed - Is First State = {isFirstState}", LSLog.LogColor.yellow);

        if (isFirstState)
        {// First state update; run initial setup
            
            InitialSetup(state);
        }
        
        GeneralMessage = state.statusMessage;

        UpdateUI(state);

        if (!isFirstState && (int)CurrentTurn != (int)state.currentTurn)
        {
            LSLog.LogImportant($"Turn Changed! - {state.currentTurn}", LSLog.LogColor.yellow);

            SetCurrentTurn((int)state.currentTurn);

            StartTurn();
        }
    }

    private void OnWorldChanged(List<DataChange> changes)
    {
        LSLog.LogImportant($"World Changed!", LSLog.LogColor.lime);
        for (int i = 0; i < changes.Count; i++)
        {
            //LSLog.LogImportant($"\tField = {changes[i].Field}", LSLog.LogColor.lime);

            if (string.Equals(changes[i].Field, "grid"))
            {
                //LSLog.LogImportant($"Update world grid! || {JsonUtility.ToJson(changes[i])}");
                //LSLog.Log($"World Grid - grid count = {ExampleManager.Instance.Room.State.world.grid.Count}");

                ReceiveWorldMap(ExampleManager.Instance.Room.State.world);
            }

        }
    }

    /// <summary>
    /// Pass the terrain map to the environment builder
    /// </summary>
    /// <param name="mapMatrix"></param>
    private void ReceiveWorldMap(World world)
    {
        environmentBuilder.BuildEnvironment(world);
    }

    /// <summary>
    /// Callback to handle the end of a player's turn
    /// </summary>
    /// <param name="wasSkip">Flag for if the turn ended because a player skipped their turn</param>
    private void OnTurnCompleted(bool wasSkip)
    {
        StartCoroutine(Co_DelayTurnEnd(wasSkip ? 0 : 2.0f));
    }

    /// <summary>
    /// Wait the delay before transitioning to next turn
    /// </summary>
    /// <param name="delay"></param>
    /// <returns></returns>
    IEnumerator Co_DelayTurnEnd(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        EndTurn();
    }

    private Player GetChallengerPlayer(TanksState state)
    {
        int challengerId = OurPlayerID == 0 ? 1 : 0;

        return state.players[challengerId];
    }

    private Player GetOurPlayer(TanksState state)
    {
        return state.players[OurPlayerID];
    }

    private void InitialSetup(TanksState state)
    {
        LSLog.LogImportant($"Initial Setup", LSLog.LogColor.lime);

        IsGameOver = false;

        // Determine our player Id
        state.players.ForEach((player) =>
        {
            if (string.Equals(player.sessionId, ExampleManager.Instance.Room.SessionId))
            {
                OurPlayerID = (int) player.playerId;
            }
        });

        SetCurrentTurn((int)state.currentTurn);

        TankController ourTank = GetOurTank();

        ourTank.UpdateSelectedWeapon(state.weapons[(int)GetOurPlayer(state).currentWeapon]);

        uiController.HideGameOverUI();
        uiController.ToggleOnlineIndicator(GetChallengerPlayer(state).connected);

        UpdatePlayerNames(state);
        UpdatePlayerHP(state);
        //UpdateUI(state);

        _waitingForFirePath = false;
        fireChargeInProgress = false;

        uiController.ToggleLoadingCover(false);

        StartTurn();
    }

    /// <summary>
    /// Callback to handle the data that comes in for the initial setup after connecting to a room or for a rematch
    /// </summary>
    /// <param name="playerTurnId">The turn Id of this client</param>
    /// <param name="playerTurn">The current player turn number</param>
    /// <param name="currentAP">The remaining Action Points of the player whose current turn it is</param>
    /// <param name="playerNames">Array of player names</param>
    /// <param name="playerHP">Array of player Hit Points</param>
    /// <param name="currentWeapon">Our tank's current weapon</param>
    /// <param name="mapMatrix">The current terrain matrix</param>
    private void OnInitialSetup(int playerTurnId, int playerTurn, int currentAP, string[] playerNames, int[] playerHP, WeaponModel currentWeapon, List<List<int>> mapMatrix, bool challengerOnline)
    {
        LSLog.LogImportant($"On Initial Setup", LSLog.LogColor.lime);

        //IsGameOver = false;

        // a negative playerTurnId indicates the setup data is for a rematch
        //if (playerTurnId >= 0)
        //{
        //    OurPlayerID = playerTurnId;
        //}

        //ReceiveWorldMap(mapMatrix);

        //SetCurrentTurn(playerTurn);

        //TankController ourTank = GetOurTank();

        //ourTank.UpdateSelectedWeapon(currentWeapon);

        // Set the AP for the active tank as received from the server
        GetTankForCurrentTurn().CurrentAP = currentAP;
        GetTankForCurrentTurn().ignoreAPReset = true;

        //uiController.HideGameOverUI();
        //uiController.ToggleOnlineIndicator(challengerOnline);

        //UpdatePlayerNames(playerNames);
        //UpdatePlayerHP(playerHP);
        //UpdateUI(ExampleManager.Instance.Room.State);

        //_waitingForFirePath = false;
        //fireChargeInProgress = false;

        StartTurn();

        //uiController.ToggleLoadingCover(false);
    }

    /// <summary>
    /// Updates the displayed player names
    /// </summary>
    /// <param name="playerNames">Array of player names</param>
    private void UpdatePlayerNames(TanksState state /*string[] playerName*/)
    {
        OurPlayerName = "";
        EnemyName = "";

        //if (playerNames != null && playerNames.Length > 1)
        //{
        //    if (OurPlayerID == 0)
        //    {
        //        OurPlayerName = playerNames[0];
        //        EnemyName = playerNames[1];
        //    }
        //    else
        //    {
        //        OurPlayerName = playerNames[1];
        //        EnemyName = playerNames[0];
        //    }
        //}
        //else if (playerNames != null && playerNames.Length > 0)
        //{
        //    OurPlayerName = playerNames[0];
        //}

        if (OurPlayerID == 0)
        {
            OurPlayerName = state.players[0].name;
            EnemyName = state.players[1].name;
        }
        else
        {
            OurPlayerName = state.players[1].name;
            EnemyName = state.players[0].name;
        }

        uiController.UpdatePlayerNames(OurPlayerName, EnemyName);

    }

    private void UpdatePlayerHP(TanksState state/*int[] playerHP*/)
    {
        if (OurPlayerID == 0)
        {
            uiController.UpdateHitPoints(/*playerHP*/(int)state.players[0].hp, true);
            uiController.UpdateHitPoints(/*playerHP*/(int)state.players[1].hp, false);
        }
        else
        {
            uiController.UpdateHitPoints(/*playerHP*/(int)state.players[1].hp, true);
            uiController.UpdateHitPoints(/*playerHP*/(int)state.players[0].hp, false);
        }

        if (/*playerHP*/(int)state.players[0].hp == 0)
        {
            GameOver(1);
        }
        else if (/*playerHP*/(int)state.players[1].hp == 0)
        {
            GameOver(0);
        }

        //if (playerHP != null && playerHP.Length > 1)
        //{
        //    if (OurPlayerID == 0)
        //    {
        //        uiController.UpdateHitPoints(playerHP[0], true);
        //        uiController.UpdateHitPoints(playerHP[1], false);
        //    }
        //    else
        //    {
        //        uiController.UpdateHitPoints(playerHP[1], true);
        //        uiController.UpdateHitPoints(playerHP[0], false);
        //    }

        //    if (playerHP[0] == 0)
        //    {
        //        GameOver(1);
        //    }
        //    else if (playerHP[1] == 0)
        //    {
        //        GameOver(0);
        //    }
        //}
        //else if (playerHP != null && playerHP.Length > 0)
        //{
        //    uiController.UpdateHitPoints(playerHP[0], true);

        //    if (playerHP[0] == 0)
        //    {
        //        GameOver(1);
        //    }
        //}

    }

    private void SetCurrentTurn(int turn)
    {
        CurrentTurn = (eTurn)turn;
    }

    /// <summary>
    /// Callback to handle player movement updates
    /// </summary>
    /// <param name="player">The turn Id of the player that has moved</param>
    /// <param name="newCoords">The new position of the player that has moved</param>
    private void OnPlayerMove(int player, Tanks.Vector2 newCoords)
    {
        TankController tank = player == 0 ? playerOneTank : playerTwoTank;
        tank.Move(new EnvironmentBuilder.MapCoordinates((int)newCoords.x, (int)newCoords.y), player == 0);

        Vector3 newWorldPos = environmentBuilder.CoordinateToWorldPosition(tank.mapCoords);
        FocusOnPosition(newWorldPos, false, null);
        UpdateUI(ExampleManager.Instance.Room.State);
    }

    /// <summary>
    /// Sets our user as "ready" for a rematch on the server
    /// </summary>
    public void RequestRematch()
    {
        attributeUpdate.Clear();
        attributeUpdate.Add("readyState", "ready");

        ExampleManager.NetSend("setAttribute", new ExampleAttributeUpdateMessage() { userId = ExampleManager.Instance.CurrentUser.sessionId, attributesToSet = attributeUpdate });
    }

    /// <summary>
    /// Quit to lobby and remove this player from the room on the server
    /// </summary>
    public void QuitMatch()
    {
        ExampleManager.CustomServerMethod("quitGame", new object[] {});

        ReturnToLobby();
    }

    /// <summary>
    /// Return to the lobby
    /// </summary>
    public void ReturnToLobby()
    {
        ExampleManager.Instance.LeaveAllRooms(() => { SceneManager.LoadScene(0); });
    }

    /// <summary>
    /// The game has ended
    /// </summary>
    /// <param name="winningPlayer">The turn Id of the player that won</param>
    public void GameOver(int winningPlayer)
    {
        uiController.ShowGameOverUI(winningPlayer);

        IsGameOver = true;
    }

    /// <summary>
    /// Initializes player tanks and start the current turn
    /// </summary>
    /// <param name="tankA">Becomes the player one tank</param>
    /// <param name="tankB">Becomes the player two tank</param>
    public void ReportTanks(GameObject tankA, GameObject tankB)
    {
        playerOneTank = tankA.GetComponent<TankController>();
        playerTwoTank = tankB.GetComponent<TankController>();
        playerOneTank.Initialize(eTurn.PLAYER_1);
        playerTwoTank.Initialize(eTurn.PLAYER_2);
        StartTurn();
    }

    public void FocusOnPlayer()
    {
        TankController activeTank = GetTankForCurrentTurn();
        FocusOnPosition(activeTank.transform.position, true, () => { activeTank.AllowAction(true); });
    }

    public void EndTurn()
    {
        fireChargeInProgress = false;
        GetTankForCurrentTurn().AbortCharge();
        GetTankForCurrentTurn().ignoreAPReset = false;
        CurrentTurn = CurrentTurn == eTurn.PLAYER_1 ? eTurn.PLAYER_2 : eTurn.PLAYER_1;
        StartTurn();

        UpdateUI(ExampleManager.Instance.Room.State);
    }

    public void FocusOnPosition(Vector3 worldPosition, bool overrideZoom, Action onArrival)
    {
        cameraManager.FocusOnPosition(worldPosition, overrideZoom, onArrival);
    }

    private void StartTurn()
    {
        fireChargeInProgress = false;
        GetTankForCurrentTurn().BeginTurn();
        GetTankForCurrentTurn().AllowAction(false);
        
        FocusOnPlayer();
    }

    void Update()
    {
        if (IsGameOver || uiController.MenuOpen)
        {
            return;
        }

        HandleInput();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            uiController.ToggleMap();
        }

        if (!IsOurTurn || _waitingForFirePath)
        {
            return;
        }

        TankController activeTank = GetTankForCurrentTurn();
        if (activeTank == null || activeTank.CanAct() == false)
            return;

        bool updateUI = activeTank.CheckHotkeys();

        if (fireChargeInProgress == false && Input.GetAxisRaw("Horizontal") != 0.0f)
        {
            MoveTank(Input.GetAxisRaw("Horizontal"));
        }

        if (Input.GetAxis("Vertical") != 0.0f)
        {
            float? tankAim = activeTank.AdjustAim(Input.GetAxis("Vertical"));

            if (tankAim != null)
            {
                SendAimAngle((float)tankAim);
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            fireChargeInProgress = true;
            GetTankForCurrentTurn().StartChargeCannon();
        }

        if (Input.GetMouseButtonDown(1))
        {
            fireChargeInProgress = false;
            GetTankForCurrentTurn().AbortCharge();
        }

        if (Input.GetMouseButtonUp(0) && GetTankForCurrentTurn().CurrentAP >= GameRules.FiringAPCost)
        {
            if (_waitingForFirePath == false && fireChargeInProgress)
            {
                RequestFirePath();
            }

            fireChargeInProgress = false;
            GetTankForCurrentTurn().AbortCharge();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            //Skip remainder of turn
            ExampleManager.NetSend("skipTurn");
        }

        //if (updateUI)
        //{
        //    UpdateUI(ExampleManager.Instance.Room.State);
        //}
    }

    /// <summary>
    /// Send a request to the server for the firing path of our tank
    /// </summary>
    private void RequestFirePath()
    {
        _waitingForFirePath = true;

        TankController tank = GetTankForCurrentTurn();

        ExampleVector3Obj barrelForward = new ExampleVector3Obj(tank.BarrelForward);
        ExampleVector3Obj barrelPosition = new ExampleVector3Obj(environmentBuilder.groundPieceRoot.InverseTransformPoint(tank.BarrelPosition));

        ExampleManager.NetSend("fireWeapon", new FireWeaponMessage() { barrelForward = barrelForward, barrelPosition = barrelPosition, cannonPower = tank.CannonPower });
        //ExampleManager.CustomServerMethod("getFirePath", new object[] { barrelForward, barrelPosition, tank.CannonPower });

        // Reset flag for waiting for fire path if the request times out
        Invoke("ResetWaitForFirePath", 5.0f);
    }

    /// <summary>
    /// Send your tank's aim angle to the server so it updates on other clients
    /// </summary>
    /// <param name="aimAngle"></param>
    private void SendAimAngle(float aimAngle)
    {
        //attributeUpdate.Clear();

        //attributeUpdate.Add("aimAngle", aimAngle.ToString());

        ExampleManager.NetSend("setAimAngle", aimAngle);

        //ExampleManager.NetSend("setAttribute", new ExampleAttributeUpdateMessage() { userId = ExampleManager.Instance.Room.SessionId/*CurrentUser.sessionId*/, attributesToSet = attributeUpdate });
    }

    /// <summary>
    /// Move our tank in the desired direction
    /// </summary>
    /// <param name="dir">The direction we want to move the tank; positive value = to the right; negative value = to the left</param>
    private void MoveTank(float dir)
    {
        if (GetTankForCurrentTurn().CanAct())
        {
            AttemptMove(dir > 0.0f ? 1 : -1);
        }
    }

    private TankController GetTankForCurrentTurn()
    {
        return CurrentTurn == eTurn.PLAYER_1 ? playerOneTank : playerTwoTank;
    }

    private TankController GetOurTank()
    {
        if (OurPlayerID == 0)
        {
            return playerOneTank;
        }

        return playerTwoTank;
    }

    private int GetCurrentTurnAP(TanksState state)
    {
        Player player = GetCurrentTurnPlayer(state);

        if (player == null)
        {
            LSLog.LogError($"Error getting player for current turn!");

            return -1;
        }

        return (int)player.currentActionPoints;
    }

    private Weapon GetCurrentTurnWeapon(TanksState state)
    {
        Player player = GetCurrentTurnPlayer(state);

        if (player == null)
        {
            LSLog.LogError($"Error getting player for current weapon!");

            return null;
        }

        return (Weapon)state.weapons[(int)player.currentWeapon];
    }

    private Player GetCurrentTurnPlayer(TanksState state)
    {
        return (Player)state.players[(int)state.currentTurn];
    }

    private void UpdateUI(TanksState state)
    {
        uiController.UpdateActionPoints(GetCurrentTurnAP(state)/*GetTankForCurrentTurn().CurrentAP*/);
        uiController.SetActiveWeaponName(IsOurTurn ? GetCurrentTurnWeapon(state).name/*GetTankForCurrentTurn().ActiveWeaponData.name*/ : "");
        uiController.SetCurrentPlayer(GetCurrentTurnPlayer(state).name/*IsOurTurn ? OurPlayerName : EnemyName*/);

        if (environmentBuilder.mapMatrix != null)
            uiController.UpdateMap(environmentBuilder);
    }

    /// <summary>
    /// Estimates a firing path for the visual trajectory line
    /// </summary>
    /// <param name="cannonPower"></param>
    /// <param name="barrelTip"></param>
    /// <returns></returns>
    public CannonController.CannonFirePath EstimateFirePath(float cannonPower, Transform barrelTip)
    {
        Vector3 initialVelocity = barrelTip.forward * cannonPower;
        Vector3 currentVelocity = initialVelocity;
        Vector3 currPos = barrelTip.position;
        List <Vector3> pathSteps = new List<Vector3>();
        pathSteps.Add(currPos);
        float grav = -0.98f;
        while (currPos.y > -1.0f)
        {
            currentVelocity.y += grav;
            currPos += currentVelocity;
            pathSteps.Add(currPos);
        }

        return environmentBuilder.TrimFirePathToEnvironment(new CannonController.CannonFirePath(pathSteps.ToArray()));
    }

    /// <summary>
    /// Sends a request to the server to attempt to move our tank in the provided direction
    /// </summary>
    /// <param name="direction"></param>
    public void AttemptMove(int direction)
    {
        ExampleManager.NetSend("movePlayer", direction);
    }

    /// <summary>
    /// For when a projectile has reached the end of its path and explodes.  Apply the damage data to the terrain and any affected players.
    /// </summary>
    /// <param name="damageData"></param>
    public void RegisterExplosion(DamageData damageData)
    {
        // Update environment
        environmentBuilder.DamageDealt(damageData);

        // Update tanks
        if (damageData != null && damageData.updatedPlayers != null)
        {
            for (int i = 0; i < damageData.updatedPlayers.Count; i++)
            {
                UpdatedPlayer updatedPlayer = damageData.updatedPlayers[i];
                
                uiController.UpdateHitPoints(updatedPlayer.remainingHP, updatedPlayer.playerId == OurPlayerID);
                
                if (updatedPlayer.playerId == 0)
                {
                    if (updatedPlayer.playerPos != null)
                    {
                        // Move the tank to the updated position
                        playerOneTank.MoveToNewCoordinates(new EnvironmentBuilder.MapCoordinates((int)((Vector2)updatedPlayer.playerPos).x, (int)((Vector2)updatedPlayer.playerPos).y), true);
                    }

                    if (updatedPlayer.remainingHP == 0)
                    {
                        // Player 1 tank has been destroyed
                        playerOneTank.Destroyed();

                        GameOver(1);
                    }
                }
                else
                {
                    if (updatedPlayer.playerPos != null)
                    {
                        // Move the tank to the updated position
                        playerTwoTank.MoveToNewCoordinates(new EnvironmentBuilder.MapCoordinates((int)((Vector2)updatedPlayer.playerPos).x, (int)((Vector2)updatedPlayer.playerPos).y), true);
                    }

                    if (updatedPlayer.remainingHP == 0)
                    {
                        // Player 2 tank has been destroyed
                        playerTwoTank.Destroyed();

                        GameOver(0);
                    }
                }
            }
        }

    }

    /// <summary>
    /// Callback to reset the bool flag after a request to get a firing path has timed out
    /// </summary>
    private void ResetWaitForFirePath()
    {
        _waitingForFirePath = false;
    }

}
