
import { Client } from "colyseus";
import { TanksRoom } from "../TanksRoom";
import { EnvironmentBuilder } from "./environmentController";
import { TurnContainer } from "./turnModel";
import { Vector2, Vector3 } from "three";

import { GameRules } from "./gameRules";
import { WeaponController } from "./weaponController";
import { Weapon } from "./weaponModel";
import { ColyseusNetworkedUser } from "../schema/ColyseusRoomState";

const logger = require("../../helpers/logger.js");

// string indentifiers for keys in the room attributes
const CurrentState = "currentGameState";
const LastState = "lastGameState";
const ClientReadyState = "readyState";
const GeneralMessage = "generalMessage";

/** Enum for game state */
const TanksServerGameState = {
    None: "None",
    Waiting: "Waiting",
    BeginRound: "BeginRound",
    SimulateRound: "SimulateRound",
    EndRound: "EndRound"
};
 
/**The object container to host the custom methods called by the client */
let tanksCustomMethods: any = {};

/**
 * The primary game loop on the server
 * @param roomRef Reference to the room
 * @param deltaTime The server delta time in seconds
 */
let gameLoop = function (roomRef: TanksRoom, deltaTime: number){

    // Update the game state
    switch (getGameState(roomRef, CurrentState)) {
        case TanksServerGameState.SimulateRound:
            simulateRoundLogic(roomRef, deltaTime);
            break;
        case TanksServerGameState.EndRound:
            endRoundLogic(roomRef, deltaTime);
            break;
        default:
            logger.error("Unknown Game State - " + getGameState(roomRef, CurrentState));
            break;
    }
}

// Client Request Logic
// These functions get called by the client in the form of the "customMethod" message set up in the room.
//======================================
/**
 * Skips the remaining turn of the current player
 * @param roomRef Reference to the room
 * @param client The client the request is coming from
 * @param request The request object from the client that will contain any parameters
 */
tanksCustomMethods.skipTurn = function(roomRef: TanksRoom, client: Client, request: any) {
    
    // Check if the player can do the action
    if(canDoAction(roomRef, client.sessionId) == false) {
        return;
    }

    endCurrentTurn(roomRef, client, true);
}

/**
 * Changes the weapon of the current player
 * @param roomRef Reference to the room
 * @param client The client the request is coming from
 * @param request The request object from the client that will contain any parameters
 */
tanksCustomMethods.changeWeapon = function(roomRef: TanksRoom, client: Client, request: any) {
    
    // Check if the player can do the action
    if(canDoAction(roomRef, client.sessionId) == false) {
        return;
    }

    if(request.param == null || request.param.length <= 0) {

        logger.error(`*** Change Weapon - Missing parameter ***`);
        return;
    }

    let desiredWeapon = Number(request.param[0]);

    if(isNaN(desiredWeapon)) {

        logger.error(`*** Error getting desired weapon - ${desiredWeapon} ***`);
        return;
    }

    const playerTurnId: number = roomRef.players.get(client.sessionId);

    const weapon: Weapon = roomRef.weaponController.switchPlayerWeapon(playerTurnId, desiredWeapon);

    if(weapon == null) {
        
        logger.error(`*** Switch Weapon - Error switching weapon ***`);
        return;
    }

    // Notify the client of the weapon change passing along the weapon's data
    client.send("selectedWeaponUpdated", { weapon });
}

/**
 * Attempts to move the current player if able
 * @param roomRef Reference to the room
 * @param client The client the request is coming from
 * @param request The request object from the client that will contain any parameters
 */
tanksCustomMethods.movePlayer = function(roomRef: TanksRoom, client: Client, request: any){
    
    // Check if the player can do the action
    if(canDoAction(roomRef, client.sessionId) == false) {
        return;
    }

    // Check if movement is possible
    if(roomRef.currentTurnContainer.currentMovement >= GameRules.MaxMovement || roomRef.currentTurnContainer.currentAP <= 0) {
        return;
    }

    if(roomRef.currentTurnContainer == null){
        roomRef.currentTurnContainer = new TurnContainer();
    }

    //0 - player number 0 = 1, 1 = 2
    //1 - moveDirection
    const param = request.param;

    // 0 = direction
    if(param == null || param.length < 1){
        throw "Error - Tank movement - Missing parameter";
        return;
    }

    let direction: number = param[0]; // the direction left/right the player desires to move
    let playerNum = roomRef.currentTurnContainer.playerTurn;

    roomRef.playerMoving = true; // Update the player moving flag to true

    // Attempt to move the player
    if(roomRef.environmentController.MovePlayer(playerNum, direction)){

        // Consume AP
        consumeAP(roomRef, GameRules.MovementAPCost);

        //This player moved successfully, let them know!
        let playerPos: Vector2 = roomRef.environmentController.GetPlayerPosition(playerNum);
        roomRef.broadcast("tankMoved", {playerNumber: playerNum, newCoords: playerPos, remainingAP: roomRef.currentTurnContainer.currentAP});

        // Check if the player has used up all their Action Points and end their turn if they have
        if (roomRef.currentTurnContainer.currentAP <= 0)
        {
            endCurrentTurn(roomRef, client);
        }
    }
}

/**
 * Calculates the path of a fired projectile and sends it to connected clients along with damage data
 * @param roomRef Reference to the room
 * @param client The client the request is coming from
 * @param request The request object from the client that will contain any parameters
 */
tanksCustomMethods.getFirePath = function(roomRef: TanksRoom, client: Client, request: any) {

    // Check if the player can do the action
    if(canDoAction(roomRef, client.sessionId) == false) {
        return;
    }

    if(roomRef.currentTurnContainer == null){
        roomRef.currentTurnContainer = new TurnContainer();
    }

    // Check if the user has enough AP to fire
    if(GameRules.FiringAPCost > roomRef.currentTurnContainer.currentAP) {
        return;
    }

    //0 - player number 0 = 1, 1 = 2
    //1 - moveDirection
    const param = request.param;

    // 0 = barrel forward | 1 = barrel position | 2 = cannon power
    if(param == null || param.length < 3){
        throw "Error - Get Fire Path - Missing parameter";
        return;
    }

    const playerNum = roomRef.currentTurnContainer.playerTurn;

    // Get the firepath using the barrel forward direction, barrel position, and the charged cannon power
    let firePath: Vector3[] = getFirePath(roomRef, new Vector3(Number(param[0].x), Number(param[0].y), Number(param[0].z)), new Vector3(Number(param[1].x), Number(param[1].y), Number(param[1].z)), Number(param[2]));
    
    // Get the player's currently active weapon
    const activeWeapon: Weapon = roomRef.weaponController.getPlayerActiveWeapon(playerNum);

    // Send the path to the environment controller to check if any damage is done to terrain or player
    let damageData: any = roomRef.environmentController.dealDamage(firePath[firePath.length - 1], activeWeapon.radius, activeWeapon.impactDamage);

    if(damageData) {
        let updatedPlayers: any[] = damageData.updatedPlayers;

        // Update player hit points
        if(updatedPlayers) {
            for(let i = 0; i < updatedPlayers.length; i++) {
                let playerId = updatedPlayers[i].playerId;
                if(roomRef.playerHP.has(playerId)) {
                    // Update player HP if there is damage
                    if(updatedPlayers[i].damage) {
                        let currentHP = roomRef.playerHP.get(playerId);
                        roomRef.playerHP.set(playerId, currentHP - updatedPlayers[i].damage);
                    }
                    
                    updatedPlayers[i].remainingHP = roomRef.playerHP.get(playerId);
                }
            }
        }
    
    }
    
    // Check player hit points
    roomRef.playerHP.forEach((hp, playerId) => {
        if(hp <= 0) {
            // Player has been destroyed!
            moveToState(roomRef, TanksServerGameState.EndRound);
        }
    });

    // Consume AP for firing weapon
    consumeAP(roomRef, GameRules.FiringAPCost);

    roomRef.broadcast("receiveFirePath", { firePath, remainingAP: roomRef.currentTurnContainer.currentAP, playerNumber: playerNum, damageData,  });

    if (roomRef.currentTurnContainer.currentAP <= 0)
    {
        logger.silly(`*** Current player has used up all AP ***`);

        endCurrentTurn(roomRef, client);
    }
}

/**
 * Quits a player from the game and room
 * @param roomRef Reference to the room
 * @param client The client the request is coming from
 * @param request The request object from the client that will contain any parameters
 */
tanksCustomMethods.quitGame = function(roomRef: TanksRoom, client: Client, request: any) {
    
    if(roomRef.players.has(client.sessionId) == false) {

        logger.error(`*** Quit Game - No player for session Id - ${client.sessionId} ***`);
        return;
    }
    
    roomRef.onPlayerQuit(client);
}
//====================================== END Client Request Logic

// GAME LOGIC
//======================================
/**
 * Checks if it is a player's turn using their session Id
 * @param roomRef Reference to the room
 * @param sessionId Session Id of the player we want to check for
 * @returns True if it is the player's turn
 */
let isPlayersTurn = function(roomRef: TanksRoom, sessionId: string): boolean {
    let playerNum = roomRef.currentTurnContainer.playerTurn;

    let playerTurnId = roomRef.players.get(sessionId);
    
    return playerNum == playerTurnId;
}

/**
 * Checks if the game is in the simulate round state and if it's the player's turn
 * @param roomRef Reference to the room
 * @param sessionId Session Id of the player we're checking
 * @returns 
 */
let canDoAction = function(roomRef: TanksRoom, sessionId: string): boolean {
    
    const notMoving = roomRef.playerMoving == false;
    const goodGameState = getGameState(roomRef, CurrentState) == TanksServerGameState.SimulateRound;
    const goodPlayer = roomRef.players.has(sessionId) && isPlayersTurn(roomRef, sessionId);

    return goodGameState && goodPlayer && notMoving;
}

/**
 * Decrements the amount from the remaining Action Points for the current turn
 * @param roomRef Reference to the room
 * @param amount The amount of Action Points to consume
 */
let consumeAP = function(roomRef: TanksRoom, amount: number)
{
    roomRef.currentTurnContainer.updateCurrentAP(-amount);
}

/**
 * Calculates and returns a projectile's path.
 * @param roomRef Reference to the room
 * @param barrelForward The direction the tank's barrel is pointing
 * @param barrelPosition The position of the muzzle of the tank's barrel
 * @param cannonPower The power charge of the tank's cannon
 * @returns An array of Vector3 resembling a projectile's path
 */
let getFirePath = function(roomRef: TanksRoom, barrelForward: Vector3, barrelPosition: Vector3, cannonPower: number) : Vector3[] {

    let initialVelocity: Vector3 = barrelForward.clone().multiplyScalar(cannonPower);
    let currentVelocity: Vector3 = initialVelocity;
    let currPos: Vector3 = barrelPosition.clone();
    let pathSteps: Vector3[] = [];
    pathSteps.push(currPos.clone());
    const grav: number = -0.98;
    while (currPos.y > -1.0)
    {
        currentVelocity.y += grav;
        currPos.add(currentVelocity);
        pathSteps.push(currPos.clone());
    }

    return getHighAccuracyFirePath(roomRef, pathSteps);
}

/**
 * Adds additional positions between each position of the original path to create a more accurate path
 * @param roomRef Reference to the room
 * @param originalPath The original path that is getting updated
 * @returns An array of Vector3 resembling a projectile's path
 */
let getHighAccuracyFirePath = function(roomRef: TanksRoom, originalPath: Vector3[]) {
    let newPath: Vector3[] = [];
    let previousPos: Vector3 = originalPath[0].clone();
    newPath.push(previousPos.clone());
    for (let i = 1; i < originalPath.length; ++i)
    {
        let currPathSeg = originalPath[i].clone();
        let dist: number = Math.floor(previousPos.distanceTo(currPathSeg)) * 2;
        let stepSize: Vector3 = new Vector3().subVectors(currPathSeg, previousPos).divideScalar(dist);

        for (let j = 0; j < dist; ++j)
        {
            previousPos.add(stepSize);
            newPath.push(previousPos.clone());
        }
    }

    return roomRef.environmentController.TrimFirePathToEnvironment(newPath);
}

/**
 * Ends the current player's turn
 * @param roomRef Reference to the room
 * @param client The client for whose turn is ending
 * @param isSkip Are we ending the turn because the player has skipped?
 */
let endCurrentTurn = function(roomRef:TanksRoom, client: Client, isSkip: boolean = false){

    if(roomRef.currentTurnContainer == null){
        roomRef.currentTurnContainer = new TurnContainer();
    }

    // Increment the current turn 
    roomRef.currentTurnContainer.incrementTurnCount();

    roomRef.setAttribute(client, {currentTurn: roomRef.currentTurnContainer.turnNumber});

    // Let everyone know this turn has ended
    roomRef.broadcast("turnComplete", {turnData: JSON.stringify(roomRef.currentTurnContainer), wasSkip: isSkip});

    roomRef.currentTurnContainer.resetActions();
}

/**
 * Returns the game state of the room
 * @param {*} roomRef Reference to the room
 * @param {*} gameState Key for which game state you want, either the Current game state for the Last game state
 */
let getGameState = function (roomRef: TanksRoom, gameState: string) : string {

    return roomRef.state.attributes.get(gameState);
}
 
/**
 * Checks if players want a rematch if they have a 'readyState' of "ready"
 * @param {*} users The collection of users from the room's state
 */
let checkForRematch = function(roomRef: TanksRoom, users: Map<string, ColyseusNetworkedUser>) {
    let playersReady: boolean = true;

    let userArr: ColyseusNetworkedUser[]  = Array.from<ColyseusNetworkedUser>(users.values());

    if(userArr.length <= 0)
        playersReady = false;

    for(let user of userArr) {
        
        let readyState: string = user.attributes.get(ClientReadyState);
        
        if(readyState == null || readyState != "ready"){
            playersReady = false;
        }
        else {
            
            let playerId: number = roomRef.players.get(user.sessionId);

            let playerName = "Player";
            if(playerId == 0) {
                playerName = roomRef.metadata.team0;
            }
            else {
                playerName = roomRef.metadata.team1;
            }

            setRoomAttribute(roomRef, GeneralMessage, `${playerName} wants a rematch!`);
        }
    }

    return playersReady;
}

/**
 * Resets data for a new round of play
 * @param roomRef Reference to the room
 */
let resetForNewRound = function (roomRef: TanksRoom) {
    
    // Generate environment
    roomRef.environmentController.GenerateEnvironment(50, 10); 

    // Reset turn data
    roomRef.currentTurnContainer.completeReset();

    // Reset weapons
    roomRef.weaponController.resetWeapons();

    // Reset HP
    roomRef.playerHP.set(0, GameRules.MaxHitPoints);
    roomRef.playerHP.set(1, GameRules.MaxHitPoints);

    setUsersAttribute(roomRef, ClientReadyState, "waiting");    
}

/**
 * Sets attribute of all connected users.
 * @param {*} roomRef Reference to the room
 * @param {*} key The key for the attribute you want to set
 * @param {*} value The value of the attribute you want to set
 */
let setUsersAttribute = function(roomRef: TanksRoom, key: string, value: string) {
    
    for(let entry of Array.from<any>(roomRef.state.networkedUsers)) {
  
        let userKey: string = entry[0];
        let msg: any = {userId: userKey, attributesToSet: {}};
  
        msg.attributesToSet[key] = value;
  
        roomRef.setAttribute(null, msg);
    }
    
  }

  /**
  * Sets attriubte of the room
  * @param {*} roomRef Reference to the room
  * @param {*} key The key for the attribute you want to set
  * @param {*} value The value of the attribute you want to set
  */
  let setRoomAttribute = function(roomRef: TanksRoom, key: string, value: string) {
    roomRef.state.attributes.set(key, value);
  }

  /**
   * Sends the data for an initial setup to the provided client
   * @param roomRef Reference to the room
   * @param client The client to send the initial setup data to
   * @param playerTurnId The turn Id of the client
   */
let sendInitialSetup = function(roomRef: TanksRoom, client: Client, playerTurnId: number) {
    const playerNames: string[] = [];

    const playerHP: number[] = [];

    if(roomRef.metadata.team0){
        playerNames.push(roomRef.metadata.team0);
        playerHP.push(roomRef.playerHP.get(0));
    }

    if(roomRef.metadata.team1) {
        playerNames.push(roomRef.metadata.team1);
        playerHP.push(roomRef.playerHP.get(1));
    }

    const initialSetup = {
        playerTurnId, 
        playerTurn: roomRef.currentTurnContainer.playerTurn, 
        currentPlayerAP: roomRef.currentTurnContainer.currentAP, 
        currentWeapon: roomRef.weaponController.weapons[0],
        worldMap: roomRef.environmentController.mapMatrix,
        playerNames,
        playerHP,
        challengerOnline: roomRef.connectedUsers > 1
    };

    // Send the initial setup data
    if(client) {
        client.send("initialSetUp", initialSetup);
    }
    else {
       roomRef.broadcast("initialSetUp", initialSetup);
    }

    // If a player has quit the game, notify the client with the name
    if(roomRef.quitPlayers.size > 0) {

        let playerName: string = "";

        roomRef.quitPlayers.forEach((pName, sessionId) => {
            playerName = pName;
        });

        // Broadcast a message to any other connected clients about the user quit
        roomRef.broadcast("onPlayerQuitGame", { playerName });
    }

}
//====================================== END GAME LOGIC

// GAME STATE LOGIC
//======================================
/**
 * Move the room game state to the new state
 * @param roomRef Reference to the room
 * @param {*} newState The new state to move to
 */
let moveToState = function (roomRef: TanksRoom, newState: string) {

    // LastState = CurrentState
    setRoomAttribute(roomRef, LastState, getGameState(roomRef, CurrentState));
            
    // CurrentState = newState
    setRoomAttribute(roomRef, CurrentState, newState);
}

/**
 * The logic run when the room is in the SimulateRound state
 * @param roomRef Reference to the room
 * @param {*} deltaTime Server delta time in seconds
 */
let simulateRoundLogic = function (roomRef: TanksRoom, deltaTime: number) {
    
    // Update the counter for player movement
    if(roomRef.playerMoving == true) {
        roomRef.currPlayerMoveWait += deltaTime;

        if(roomRef.currPlayerMoveWait >= GameRules.MovementTime) {
            roomRef.playerMoving = false;
            roomRef.currPlayerMoveWait = 0;
        }
    }

}

/**
 * The logic run when the room is in the EndRound state
 * @param roomRef Reference to the room
 * @param {*} deltaTime Server delta time in seconds
 */
let endRoundLogic = function (roomRef:TanksRoom, deltaTime: number) {

    // Check if all the users are ready for a rematch
    let playersReady = checkForRematch(roomRef, roomRef.state.networkedUsers);

    // Return out if not all of the players are ready yet.
    if(playersReady == false) { 
        
        return;
    }

    setRoomAttribute(roomRef, GeneralMessage, "");

    resetForNewRound(roomRef);

    // Send the new setup data to all clients with a player turn Id of -1 to represent the beginning of a new round
    sendInitialSetup(roomRef, null, -1);

    // Begin a new round
    moveToState(roomRef, TanksServerGameState.SimulateRound);
}
//====================================== END GAME STATE LOGIC

// Room accessed functions
//======================================
/**
 * Initialize the Tanks logic
 * @param {*} roomRef Reference to the room
 * @param {*} options Options of the room from the client when it was created
 */
exports.InitializeLogic = function (roomRef: TanksRoom, options: any) {

    roomRef.environmentController = new EnvironmentBuilder();
    roomRef.currentTurnContainer = new TurnContainer();
    roomRef.players = new Map<string, number>();  
    roomRef.weaponController = new WeaponController();
    roomRef.playerHP = new Map<number, number>();

    // Set initial game state to waiting for all clients to be ready
    setRoomAttribute(roomRef, CurrentState, TanksServerGameState.SimulateRound)
    setRoomAttribute(roomRef, LastState, TanksServerGameState.None);

    resetForNewRound(roomRef);
}

/**
 * Run Game Loop Logic
 * @param {*} roomRef Reference to the room
 * @param {*} deltaTime Server delta time in milliseconds
 */
exports.ProcessLogic = function (roomRef: TanksRoom, deltaTime: number) {
    
    gameLoop(roomRef, deltaTime / 1000); // convert milliseconds to seconds
}

/**
 * Processes requests from a client to run custom methods
 * @param {*} roomRef Reference to the room
 * @param {*} client Reference to the client the request came from
 * @param {*} request Request object holding any data from the client
 */ 
exports.ProcessMethod = function (roomRef: TanksRoom, client: any, request: any) {
    
    // Check for and run the method if it exists
    if (request.method in tanksCustomMethods && typeof tanksCustomMethods[request.method] === "function") {
        
        tanksCustomMethods[request.method](roomRef, client, request);
    } else {
        throw "No Method: " + request.method + " found";
        return; 
    }
}

/**
 * Process a user joinging the room
 * @param {*} roomRef Reference to the room
 * @param {*} client Reference to the client the request came from
 * @param {*} options Any options for the user
 */
exports.ProcessUserJoined = function (roomRef: TanksRoom, client: Client, options: any){

    const playerTurnId = roomRef.players.get(client.sessionId);
    let playerName: string = "";

    if(playerTurnId == 0) {
        playerName = roomRef.metadata.team0;
    }
    else {
        playerName = roomRef.metadata.team1;
    }

    // Send this client the current data
    sendInitialSetup(roomRef, client, playerTurnId);

    // Let other client know that another player has joined
    roomRef.broadcast("playerJoined", { playerName, playerId: playerTurnId }, { except: client });
}
//====================================== END VME Room accessed functions