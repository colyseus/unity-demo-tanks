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

    public static TankGameManager Instance { get; private set; }

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

    public int OurPlayerID { get; private set; } = -1;
    public string OurPlayerName { get; private set; }
    public string EnemyName { get; private set; }
    public string GeneralMessage { get; private set; }
    public bool IsGameOver { get; private set; } = false;

    private Dictionary<Projectile, ProjectileBase> _projectileObjects = new Dictionary<Projectile, ProjectileBase>();

    private bool _fireChargeInProgress = false;
    private bool _runInitialSetupForRematch = false;

    private GameObject _projectileTarget;

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

    void OnEnable()
    {
        // Subscribe to events
        ExampleRoomController.onRoomStateChanged += OnRoomStateChanged;
        ExampleRoomController.onTankMoved += OnPlayerMove;
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
        ExampleRoomController.onTankMoved -= OnPlayerMove;
        ExampleRoomController.onWorldChanged -= OnWorldChanged;
        ExampleRoomController.onPlayerChange -= OnPlayerUpdated;
        ExampleRoomController.onProjectileAdded -= OnProjectileAdded;
        ExampleRoomController.onProjectileRemoved -= OnProjectileRemoved;
        ExampleRoomController.onProjectileUpdated -= OnProjectileUpdated;
        ExampleRoomController.onWorldGridChanged -= OnWorldGridChanged;
    }

    private void OnWorldGridChanged(string index, float value)
    {
        environmentBuilder.UpdateChangedGridCoordinate(index, value);
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

            _projectileTarget = proj.gameObject;
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

            if (IsOurTurn == false && proj.gameObject == _projectileTarget)
            {
                StartCoroutine(Co_WaitAndRefocusPlayer());
            }

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
        for (int i = 0; i < changes.Count; i++)
        {
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
            case "currentActionPoints":
                tank.CurrentAP = (int) ((float) change.Value);
                break;
            case "hp":
                UpdatePlayerHP(state);

                if ((float) change.Value == 0)
                {
                    tank.gameObject.SetActive(false);
                }

                break;
        }

        UpdateUI(state);
    }

    private void OnPlayerQuit(string quittingPlayerName)
    {
        string quitMsg = IsGameOver ? $"{quittingPlayerName} has left" : $"{quittingPlayerName} Surrendered!";

        uiController.ShowGameOverUI(OurPlayerID, quitMsg);
        
        IsGameOver = true;
    }

    /// <summary>
    /// Callback for the room state change
    /// </summary>
    /// <param name="attributes"></param>
    private void OnRoomStateChanged(TanksState state, bool isFirstState)
    {
        //LSLog.LogImportant($"On Room State Changed - Is First State = {isFirstState}", LSLog.LogColor.yellow);

        if (isFirstState || _runInitialSetupForRematch)
        {// First state update; run initial setup

            InitialSetup(state);
        }
        
        GeneralMessage = state.statusMessage;

        UpdateUI(state);

        // Current turn has changed
        if (!isFirstState && (int)CurrentTurn != (int)state.currentTurn)
        {
            SetCurrentTurn((int)state.currentTurn);

            StartTurn();
        }

        // Check if someone has quit the match
        if (state.inProcessOfQuitingGame)
        {
            // Get the name of the other player that quit
            int otherPlayerId = OurPlayerID == 0 ? 1 : 0;

            OnPlayerQuit(state.players[otherPlayerId].name);
        }
    }

    private void OnWorldChanged(List<DataChange> changes)
    {
        for (int i = 0; i < changes.Count; i++)
        {
            if (string.Equals(changes[i].Field, "grid"))
            {
                // We got a new world
                ReceiveWorldMap(ExampleManager.Instance.Room.State.world);

                // If we're currently in a Game Over State it means we're starting a new round of play
                if (IsGameOver)
                {
                    _runInitialSetupForRematch = true;
                }
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
        IsGameOver = false;
        _runInitialSetupForRematch = false;

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
        
        _fireChargeInProgress = false;

        uiController.ToggleLoadingCover(false);

        StartTurn();

        ExampleRoomController.onWorldGridChanged += OnWorldGridChanged;
    }

    /// <summary>
    /// Updates the displayed player names
    /// </summary>
    /// <param name="playerNames">Array of player names</param>
    private void UpdatePlayerNames(TanksState state)
    {
        OurPlayerName = "";
        EnemyName = "";

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

    private void UpdatePlayerHP(TanksState state)
    {
        if (OurPlayerID == 0)
        {
            uiController.UpdateHitPoints((int)state.players[0].hp, true);
            uiController.UpdateHitPoints((int)state.players[1].hp, false);
        }
        else
        {
            uiController.UpdateHitPoints((int)state.players[1].hp, true);
            uiController.UpdateHitPoints((int)state.players[0].hp, false);
        }

        if ((int)state.players[0].hp == 0)
        {
            GameOver(1);
        }
        else if ((int)state.players[1].hp == 0)
        {
            GameOver(0);
        }
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
        FocusOnPosition(newWorldPos, overrideZoom: false, onArrival: null);
        UpdateUI(ExampleManager.Instance.Room.State);
    }

    /// <summary>
    /// Sets our user as "ready" for a rematch on the server
    /// </summary>
    public void RequestRematch()
    {
        ExampleManager.NetSend("requestRematch");
    }

    /// <summary>
    /// Quit to lobby and remove this player from the room on the server
    /// </summary>
    public void QuitMatch()
    {
        ExampleManager.NetSend("quitGame");

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

        // Unsubscribe from grid changes until the next game gets initialized
        ExampleRoomController.onWorldGridChanged -= OnWorldGridChanged;
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
        FocusOnPosition(activeTank.transform.position, zoom: 0.75f, false, onArrival: () => { activeTank.AllowAction(true); });
    }

    public void FocusOnPosition(Vector3 worldPosition, float? zoom = null, bool overrideZoom = false, Action onArrival = null)
    {
        cameraManager.FocusOnPosition(worldPosition, targetZoom: zoom, overrideZoom: overrideZoom, onArrival: onArrival);
    }

    private void StartTurn()
    {
        _fireChargeInProgress = false;
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

        if (IsOurTurn == false && _projectileTarget)
        {
            cameraManager.FocusOnPosition(_projectileTarget.transform.position, 0.5f);
        }
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            uiController.ToggleMap();
        }

        if (!IsOurTurn)
        {
            return;
        }

        TankController activeTank = GetTankForCurrentTurn();
        if (activeTank == null || activeTank.CanAct() == false)
            return;

        activeTank.CheckHotkeys();

        if (_fireChargeInProgress == false && Input.GetAxisRaw("Horizontal") != 0.0f)
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

        if (GetCurrentTurnAP(ExampleManager.Instance.Room.State) >= GameRules.FiringAPCost && Input.GetMouseButtonDown(0))
        {
            _fireChargeInProgress = true;
            GetTankForCurrentTurn().StartChargeCannon();
        }

        if (Input.GetMouseButtonDown(1))
        {
            _fireChargeInProgress = false;
            GetTankForCurrentTurn().AbortCharge();
        }

        if (Input.GetMouseButtonUp(0) && GetTankForCurrentTurn().CurrentAP >= GameRules.FiringAPCost)
        {
            if (_fireChargeInProgress)
            {
                AttemptWeaponFire();
            }

            _fireChargeInProgress = false;
            GetTankForCurrentTurn().AbortCharge();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            //Skip remainder of turn
            ExampleManager.NetSend("skipTurn");
        }
    }

    /// <summary>
    /// Send a request to the server to fire our weapon
    /// </summary>
    private void AttemptWeaponFire()
    {
        TankController tank = GetTankForCurrentTurn();

        ExampleVector3Obj barrelForward = new ExampleVector3Obj(tank.BarrelForward);
        ExampleVector3Obj barrelPosition = new ExampleVector3Obj(environmentBuilder.groundPieceRoot.InverseTransformPoint(tank.BarrelPosition));

        ExampleManager.NetSend("fireWeapon", new FireWeaponMessage() { barrelForward = barrelForward, barrelPosition = barrelPosition, cannonPower = tank.CannonPower });
        
    }

    /// <summary>
    /// Send your tank's aim angle to the server so it updates on other clients
    /// </summary>
    /// <param name="aimAngle"></param>
    private void SendAimAngle(float aimAngle)
    {
        ExampleManager.NetSend("setAimAngle", aimAngle);
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
        uiController.UpdateActionPoints(GetCurrentTurnAP(state));
        uiController.SetActiveWeaponName(GetCurrentTurnWeapon(state).name);
        uiController.SetCurrentPlayer(GetCurrentTurnPlayer(state).name);

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

    private IEnumerator Co_WaitAndRefocusPlayer()
    {
        yield return new WaitForSeconds(1.0f);

        FocusOnPlayer();
    }
}
