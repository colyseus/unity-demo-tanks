"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    Object.defineProperty(o, k2, { enumerable: true, get: function() { return m[k]; } });
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || function (mod) {
    if (mod && mod.__esModule) return mod;
    var result = {};
    if (mod != null) for (var k in mod) if (k !== "default" && Object.hasOwnProperty.call(mod, k)) __createBinding(result, mod, k);
    __setModuleDefault(result, mod);
    return result;
};
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.TanksRoom = void 0;
const colyseus_1 = require("colyseus");
const ColyseusRoomState_1 = require("./schema/ColyseusRoomState");
const logger = require("../helpers/logger");
const weaponController_1 = require("./customLogic/weaponController");
class TanksRoom extends colyseus_1.Room {
    constructor() {
        super(...arguments);
        this.serverTime = 0;
        this.customMethodController = null;
        this.playerMoving = false; // Flag to track when a player has moved as an action
        this.currPlayerMoveWait = 0; // Counter to help with the wait before allowing another action after the player has moved
        this.connectedUsers = 0; // The number of actively connected users; used to determine if the simulation loop and patch update should be paused
        this.pauseDelay = 2140000000; // 24+ days // Pause length when there are no actively connected users
        this.patchDelay = 50; // The ms delay between room state patch updates
        this.firstJoin = true; // Flag used to check if a joining user is first to join after all previous users had left the room
        this.inProcessOfQuitingGame = false; // Flag to help track when a player has quit the game
    }
    /**
     * Getter function to retrieve the correct customLogic file. Will try .JS extension and then .TS
     * @param {*} fileName
     */
    getCustomLogic(fileName) {
        return __awaiter(this, void 0, void 0, function* () {
            try {
                this.customMethodController = yield Promise.resolve().then(() => __importStar(require('./customLogic/' + fileName)));
            }
            catch (e) {
                logger.error(e);
            }
            return this.customMethodController;
        });
    }
    /**
     * Callback for the "customMethod" message from the client to run a custom function within the custom logic.
     * Function name is sent from a client.
     * @param {*} client
     * @param {*} request
     */
    onCustomMethod(client, request) {
        try {
            if (this.customMethodController != null) {
                this.customMethodController.ProcessMethod(this, client, request);
            }
            else {
                logger.debug("NO Custom Method Logic Set");
            }
        }
        catch (error) {
            logger.error("Error with custom Method logic: " + error);
        }
    }
    /**
     * Callback for when the room is created
     * @param {*} options The room options sent from the client when creating a room
     */
    onCreate(options) {
        return __awaiter(this, void 0, void 0, function* () {
            logger.info("*********************** TANKS ROOM CREATED ***********************");
            console.log(options);
            logger.info("***********************");
            this.maxClients = 2;
            this.autoDispose = false;
            this.roomOptions = options;
            // Initialize collections
            this.quitPlayers = new Map();
            this.weaponController = new weaponController_1.WeaponController();
            this.players = new Map();
            this.playerSessions = new Map();
            this.playerHP = new Map();
            if (options["roomId"] != null) {
                this.roomId = options["roomId"];
            }
            this.playerSessions = new Map();
            if (options["creatorId"] != null) {
                logger.silly(options["creatorId"] + " started a new game");
                this.setMetadata({ "team0": options["creatorId"] });
            }
            // Set the room state
            this.setState(new ColyseusRoomState_1.ColyseusRoomState());
            // Set the callback for the "ping" message for tracking server-client latency
            this.onMessage("ping", (client) => {
                client.send(0, { serverTime: this.serverTime });
            });
            // Set the callback for the "customMethod" message
            this.onMessage("customMethod", (client, request) => {
                this.onCustomMethod(client, request);
            });
            // Set the callback for the "removeFunctionCall" message
            this.onMessage("remoteFunctionCall", (client, RFCMessage) => {
                //Confirm Sending Client is Owner 
                RFCMessage.clientId = client.id;
                // Broadcast the "remoteFunctionCall" to all clients except the one the message originated from
                this.broadcast("onRFC", RFCMessage, RFCMessage.target == 0 ? {} : { except: client });
            });
            // Set the callback for the "setAttribute" message to set an entity or user attribute
            this.onMessage("setAttribute", (client, attributeUpdateMessage) => {
                this.setAttribute(client, attributeUpdateMessage);
            });
            // Set the frequency of the patch rate
            this.setPatchRate(this.patchDelay);
            // Retrieve the custom logic for the room
            this.customLogic = yield this.getCustomLogic(options["logic"]);
            if (this.customLogic == null)
                logger.debug("NO Custom Logic Set");
            try {
                if (this.customLogic != null)
                    this.customLogic.InitializeLogic(this, options);
            }
            catch (error) {
                logger.error("Error with custom room logic: " + error);
            }
            // Set the Simulation Interval callback
            this.setSimulationInterval(dt => { this.intervalSimulation(this, dt); });
        });
    }
    // Callback when a client has joined the room
    onJoin(client, options) {
        logger.info(`Client joined!- ${client.sessionId} ***`);
        // Check if user is first to join after all previous users had left
        if (this.firstJoin == false && this.connectedUsers <= 0) {
            // Reset sim interval and patch updates
            // Reset the frequency of the patch rate
            this.setPatchRate(this.patchDelay);
            // Reset the Simulation Interval callback
            this.setSimulationInterval(dt => { this.intervalSimulation(this, dt); });
        }
        // Increment connected users
        this.connectedUsers++;
        this.firstJoin = false;
        let newNetworkedUser = new ColyseusRoomState_1.ColyseusNetworkedUser().assign({
            id: client.id,
            sessionId: client.sessionId,
        });
        console.log(options);
        let joiningId = options["joiningId"];
        if (joiningId == null) {
            joiningId = options["creatorId"];
            if (joiningId == null) {
                throw "*** On Join - Missing parameter ***";
                return;
            }
        }
        // Need to update the networked users before updating the player sessions
        this.updateNetworkedUser(joiningId, client.sessionId, newNetworkedUser);
        let turnId = -1;
        // If it belongs to the room creator update their session Id
        if (this.metadata.team0 == joiningId) {
            turnId = 0;
        }
        // If it belongs to a returning challenger update their session Id
        else if (this.metadata.team1 == joiningId) {
            turnId = 1;
        }
        // It belongs to a new challenger; do first time set up
        else {
            this.setMetadata({ "team1": options["joiningId"] });
            turnId = 1;
        }
        // Track this player in the weapon controller
        this.weaponController.addPlayer(turnId);
        // Update the session Id for this player
        this.updatePlayerSession(joiningId, client.sessionId, turnId);
        this.setAttribute(client, { userId: client.sessionId, attributesToSet: { turnId: turnId.toString() } });
        client.send("onJoin", newNetworkedUser);
        if (this.customMethodController != null) {
            if (this.customMethodController.ProcessUserJoined != null)
                this.customMethodController.ProcessUserJoined(this, client, options);
        }
    }
    /**
     * Updates the session Id of a returning player
     * @param joiningId The player name created when the player first started
     * @param sessionId The client session Id of the joining player
     * @param playerTurnId The turn Id of the joining player
     */
    updatePlayerSession(joiningId, sessionId, playerTurnId) {
        if (this.playerSessions.has(joiningId)) {
            // Remove a previous session if it exists
            const previousSessionId = this.playerSessions.get(joiningId);
            if (this.players.has(previousSessionId)) {
                this.players.delete(previousSessionId);
            }
        }
        this.players.set(sessionId, playerTurnId);
        this.playerSessions.set(joiningId, sessionId);
    }
    /**
     * Updates the networked user object of a returning player
     * @param joiningId The player name created when the player first started
     * @param newSessionId The client session Id of the joining player
     * @param networkedUser The new networkedUser object
     */
    updateNetworkedUser(joiningId, newSessionId, networkedUser) {
        // Set the new networked user
        this.state.networkedUsers.set(newSessionId, networkedUser);
        if (this.playerSessions.has(joiningId)) {
            // Get the previous session Id of the user
            const previousSessionId = this.playerSessions.get(joiningId);
            // Remove the networked user associated with the previous session if it exists
            if (this.state.networkedUsers.has(previousSessionId)) {
                this.state.networkedUsers.delete(previousSessionId);
            }
        }
    }
    /**
     * Run the simulation
     * @param roomRef Reference to the room
     * @param dt Delta time of the server
     */
    intervalSimulation(roomRef, dt) {
        roomRef.serverTime += dt;
        //Run Custom Logic for room if loaded
        try {
            if (roomRef.customLogic != null) {
                roomRef.customLogic.ProcessLogic(roomRef, dt);
            }
        }
        catch (error) {
            logger.error("Error with custom room logic: " + error);
        }
    }
    /**
     * Set the attribute of an entity or a user
     * @param {*} client
     * @param {*} attributeUpdateMessage
     */
    setAttribute(client, attributeUpdateMessage) {
        if (attributeUpdateMessage == null
            || (attributeUpdateMessage.entityId == null && attributeUpdateMessage.userId == null)
            || attributeUpdateMessage.attributesToSet == null) {
            return; // Invalid Attribute Update Message
        }
        // Set user attribute
        else if (attributeUpdateMessage.userId) {
            //Check is this client ownes the object
            if (this.state.networkedUsers.has(`${attributeUpdateMessage.userId}`) === false) {
                logger.error(`Set Attribute - User Attribute - Room does not have networked user with Id - \"${attributeUpdateMessage.userId}\"`);
                return;
            }
            this.state.networkedUsers.get(`${attributeUpdateMessage.userId}`).timestamp = parseFloat(this.serverTime.toString());
            let userAttributes = this.state.networkedUsers.get(`${attributeUpdateMessage.userId}`).attributes;
            for (let index = 0; index < Object.keys(attributeUpdateMessage.attributesToSet).length; index++) {
                let key = Object.keys(attributeUpdateMessage.attributesToSet)[index];
                let value = attributeUpdateMessage.attributesToSet[key];
                userAttributes.set(key, value);
            }
        }
    }
    // Callback when a client has left the room
    onLeave(client, consented) {
        return __awaiter(this, void 0, void 0, function* () {
            this.connectedUsers--;
            if (this.connectedUsers <= 0) {
                logger.info(`*** No Connected Users - Pausing Server Updates ***`);
                // Pause the server when there are no connected users
                // Set the frequency of the patch rate
                this.setPatchRate(this.pauseDelay);
                this.setSimulationInterval(dt => { this.intervalSimulation(this, dt); }, this.pauseDelay);
            }
            // User has left room after a game quit has been started; quit this user
            if (this.inProcessOfQuitingGame && this.quitPlayers.has(client.sessionId) == false) {
                this.onPlayerQuit(client);
            }
            this.broadcast("onPlayerLeave", {});
        });
    }
    /**
     * Handle a player quitting and the clean up involved with it
     * @param client The client of the player that quit.
     * @returns
     */
    onPlayerQuit(client) {
        let networkedUser = this.state.networkedUsers.get(client.sessionId);
        if (networkedUser == null) {
            logger.error(`*** On Player Quit - No Networked User for session Id - ${client.sessionId} ***`);
            return;
        }
        // Flag for if the room should disconnect after this player has quit
        let disconnectRoom = false;
        // Set the user to not connected
        networkedUser.connected = false;
        // Get the turn Id of the user
        const playerTurnId = this.players.get(client.sessionId);
        // Get the player name of the user
        let playerName;
        if (playerTurnId == 0) {
            playerName = this.metadata.team0;
            this.setMetadata({ team0: `${playerName} (Surrendered)` });
        }
        else {
            playerName = this.metadata.team1;
            this.setMetadata({ team1: `${playerName} (Surrendered)` });
        }
        // Remove user from weapon controller
        this.weaponController.removePlayer(playerTurnId);
        // Remove user from playerSessions collection
        if (this.playerSessions.has(playerName)) {
            this.playerSessions.delete(playerName);
        }
        else {
            logger.error(`*** On Player Quit - No player session for player - ${playerName} ***`);
        }
        // Remove user from players collection
        if (this.players.has(client.sessionId)) {
            this.players.delete(client.sessionId);
        }
        else {
            logger.error(`*** On Player Quit - No player turn Id for user - ${client.sessionId} ***`);
        }
        // Remove user from the room's networked user collection
        if (this.state.networkedUsers.has(client.sessionId)) {
            this.state.networkedUsers.delete(client.sessionId);
        }
        else {
            logger.error(`*** On Player Quit - No networked user for user - ${client.sessionId}`);
        }
        // Broadcast a message to any other connected clients about the user quit
        this.broadcast("onPlayerQuitGame", { playerName }, { except: client });
        // Check if creator has quit before anyone else has joined
        if (this.metadata.team0 && this.metadata.team1 == null) {
            disconnectRoom = true;
        }
        // No other users are in the room so disconnect
        if (this.inProcessOfQuitingGame && this.state.networkedUsers.size <= 1 && this.connectedUsers <= 1) {
            disconnectRoom = true;
        }
        // Set to true so that if any other player joins after the this one quits they'll be alerted in game and can then leave
        this.inProcessOfQuitingGame = true;
        this.quitPlayers.set(client.sessionId, playerName);
        // Should the room disconnect?
        if (disconnectRoom) {
            this.disconnect();
        }
    }
    onDispose() {
    }
}
exports.TanksRoom = TanksRoom;
