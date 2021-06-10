import { Room, Client } from "colyseus";
import { TanksState, GameState } from "./schema/TanksState";
import { Player, PlayerReadyState } from "./schema/Player";
import { EnvironmentBuilder } from "./tanks/EnvironmentController";
import { GameRules } from "./tanks/rules";
import { Vector3, Vector2 as Vector_2 } from "three";
import logger from "../helpers/logger";

export class TanksRoom extends Room<TanksState> {
    
    environmentController: EnvironmentBuilder; // Generates and maintains the game's terrain
    currPlayerActionWait: number = 0; // Counter to help with the wait before allowing another action
    
    patchRate = 50; // The ms delay between room state patch updates

    /**
     * Callback for when the room is created
     * @param {*} options The room options sent from the client when creating a room
     */
    async onCreate(options: any) {
        console.info("*********************** TANKS ROOM CREATED ***********************");
        console.log(options);
        console.info("***********************");

        this.maxClients = 2;
        this.autoDispose = false;
        this.roomId = options["roomId"];

        // Initialize initial room state
        this.setState(new TanksState().assign({
            gameState: GameState.SimulateRound,
            previousGameState: GameState.None,
            creatorId: options["creatorId"]
        }));

        // pre-populate players by "playerId"
        // - state.players[0] is playerId=0
        // - state.players[1] is playerId=1
        this.state.players.push(new Player().assign({
            name: options["creatorId"],
            playerId: 0
        }));
        this.state.players.push(new Player().assign({
            playerId: 1
        }));

        this.environmentController = new EnvironmentBuilder(this.state);

        this.state.environmentBuilder = this.environmentController;

        this.resetForNewRound();

        this.registerMessageHandlers();

        // Set the Simulation Interval callback
        this.setSimulationInterval(dt => this.gameLoop(dt));
    }

    // Callback when a client has joined the room
    onJoin(client: Client, options: any) {
       
        const username = options["joiningId"] || options["creatorId"];

        const isCreator = this.state.creatorId === username; 

        // attach custom data to the client.
        client.userData = {
            playerId: (isCreator) ? 0 : 1
        };

        const player: Player = this.state.players[client.userData.playerId];

        let playerSetting = {};

        if(isCreator) {
            // Just update the sessionId of the creator player
            playerSetting = {
                sessionId: client.sessionId,
                connected: true
            }
        }
        else {
            // Update relevant data for challenger player
            playerSetting = {
                sessionId: client.sessionId,
                name: username,
                connected: true
            }
        }

        // Assign data to be synchronized
        player.assign(playerSetting);

        // Set team0 / team1 key on room's metadata
        this.setMetadata({
            [`team${player.playerId}`]: player.name
        });
    }

    /**
     * Run the game loop
     * @param deltaTime Delta time of the server
     */
    gameLoop(deltaTime: number) {

        const deltaTimeSeconds = deltaTime / 1000;

        // Update the game state
        switch (this.state.gameState) {
            case GameState.SimulateRound:
                // Update the counter for player movement
                if (this.state.isPlayerActing == true) {
                    this.currPlayerActionWait += deltaTimeSeconds;

                    if (this.currPlayerActionWait >= GameRules.MovementTime && this.state.isWaitingForProjectile == false) {
                        this.state.isPlayerActing = false;
                        this.currPlayerActionWait = 0;
                        
                        // Check if the player has used up all their Action Points and end their turn if they have
                        if (this.state.getCurrentTurnPlayer().currentActionPoints <= 0) {
                            this.state.nextTurn();
                        }
                    }
                }

                this.updateProjectiles(deltaTimeSeconds);
                break;

            case GameState.EndRound:
                // Check if all the users are ready for a rematch
                let playersReady = this.checkForRematch();

                // Return out if not all of the players want a rematch yet.
                if (playersReady === false) { return; }

                this.state.statusMessage = "";

                this.resetForNewRound();

                // Begin a new round
                this.state.moveToState(GameState.SimulateRound);

                break;

            default:
                console.error("Unknown Game State - " + this.state.gameState);
                break;
        }

    }

    /**
     * Updates any projectiles along their path
     * @param deltaTime Server delta time in seconds
     * @returns 
     */
    private updateProjectiles(deltaTime: number){

        if(this.state.projectiles == null) {
            return;
        }

        this.state.projectiles.forEach((projectile, key) =>{

            projectile.updateProjectileAlongPath(deltaTime);
        });
    }

    /**
     * Registers handlers for messages expected to come from the client
     */
    private registerMessageHandlers() {

        /**
         * Message handler for when a player wants a rematch
         */
        this.onMessage("requestRematch", (client) => {

            const player = this.state.players[client.userData.playerId];
            if (player) {
                player.readyState = PlayerReadyState.REMATCH;

                this.state.statusMessage = `${player.name} wants a rematch!`;
            }
        });

        /**
         * Message handler for when a player elects to skip their remaining turn
         */
        this.onMessage("skipTurn", (client, message) => {
            // Check if the player can do the action
            if (this.canDoAction(client.userData.playerId) == false) {
                return;
            }

            this.state.nextTurn();
        });

        /**
         * Message handler when a player changes their selected weapon
         */
        this.onMessage("changeWeapon", (client: Client, message) => {
            // Check if the player can do the action
            if (this.canDoAction(client.userData.playerId) == false) {
                return;
            }

            this.state.switchPlayerWeapon(client.userData.playerId, message);
        });

        /** 
         * Message handler for when a player adjusts the aim angle of their tank
         */
        this.onMessage("setAimAngle", (client: Client, message) => {
            // Check if the player can do the action
            if (this.canDoAction(client.userData.playerId) == false) {
                return;
            }

            this.state.setAimAngle(client.userData.playerId, message);
        });

        /**
         * Message handler for when a player wants to move their tank
         */
        this.onMessage("movePlayer", (client: Client, direction) => {
            // Skip if the player cannot do the action
            if (this.canDoAction(client.userData.playerId) === false) { return; }

            const player = this.state.players[client.userData.playerId];

            // Skip if movement is not possible
            if (!player.isMovementAllowed()) { return; }

            this.state.isPlayerActing = true; // Update the player acting flag to true

            const nextPosition = this.environmentController.GetAvailableSpace(direction, player.coords);
            const canMove: boolean = player.coords.x !== nextPosition.x;

            // Attempt to move the player
            if (canMove) {

                // update player coords
                this.environmentController.SetPlayerPosition(player.playerId, nextPosition);

                // Consume AP
                player.consumeActionPoints(GameRules.MovementActionPointCost);

            }
        });

        /**
         * Message handler for when a user wants to fire their weapon
         */
        this.onMessage("fireWeapon", (client, message) => {

            // Check if the player can do the action
            if (this.canDoAction(client.userData.playerId) == false) {

                logger.info(`*** Player can't do action - ${client.userData.playerId} ***`);
                return;
            }

            const player = this.state.players[client.userData.playerId];

            // skip if the user does not have enough Action Points to fire
            if (player.currentActionPoints < GameRules.FiringActionPointCost) { 
                
                logger.info(`*** Player does not have enough AP! - ${client.userData.playerId} ***`);
                return; 
            }

            this.state.isPlayerActing = true; // Update the player acting flag to true
            this.state.isWaitingForProjectile = true; // Update the wait for projectile flag to true

            const barrelForward = message.barrelForward;
            const barrelPosition = message.barrelPosition;
            const cannonPower = message.cannonPower;

            // validate message input
            if (
                barrelForward === undefined ||
                barrelPosition === undefined ||
                typeof (cannonPower) !== "number"
            ) {
                throw "Error - Get Fire Path - Missing parameter";
            }

            // Get the firepath using the barrel forward direction, barrel position, and the charged cannon power
            let projectilePath: Vector_2[] = this.state.environmentBuilder.getFirePath(
                new Vector3(barrelForward.x, barrelForward.y, barrelForward.z),
                new Vector3(barrelPosition.x, barrelPosition.y, barrelPosition.z),
                cannonPower
            );

            this.state.addNewProjectile(client.userData.playerId, projectilePath);
            
            // Consume AP for firing weapon
            player.consumeActionPoints(GameRules.FiringActionPointCost);
        });

        /**
         * Message handler for when a user has elected to quit a game in progress surrendering the game to the other player if one exists
         */
        this.onMessage("quitGame", (client, message) => {
            const quittingPlayer = this.state.players[client.userData.playerId];
            if (!quittingPlayer) {
                console.error(`*** onLeave - No Player for sessionId - ${client.sessionId} ***`);
                return;
            }

            this.onPlayerQuit(quittingPlayer);
        });
    }

    /**
     * Resets data for a new round of play
     */
    private resetForNewRound() {

        this.currPlayerActionWait = 0;

        // Generate new environment
        this.environmentController.GenerateEnvironment(50, 10);

        // Reset turn data
        this.state.restart();
    }

    /**
     * Checks if the game is in the simulate round state and if it's the player's turn
     * @param playerId Session Id of the player we're checking
     * @returns 
     */
    private canDoAction(playerId: number): boolean {
        const notActing = this.state.isPlayerActing === false;
        const notWaitingForProjectile = this.state.isWaitingForProjectile === false;
        const goodGameState = (this.state.gameState === GameState.SimulateRound);

        return (
            goodGameState && 
            playerId === this.state.currentTurn &&
            notActing &&
            notWaitingForProjectile
        );
    }

    /**
     * Checks if players want a rematch if they have a 'readyState' of "watsRematch"
     */
    private checkForRematch(): boolean {
        let numPlayersReady: number = 0;

        this.state.players.forEach((player) => {
            if (player.readyState === PlayerReadyState.REMATCH) {
                numPlayersReady++;
            }
        });

        return (numPlayersReady === 2);
    }

    // Callback when a client has left the room
    async onLeave(client: Client, consented: boolean) {
        const leavingPlayer = this.state.players[client.userData.playerId];
        if (!leavingPlayer) {
            console.error(`*** onLeave - No Player for sessionId - ${client.sessionId} ***`);
            return;
        }

        // Sync player as not connected.
        leavingPlayer.connected = false;

        if(this.state.inProcessOfQuitingGame  && this.state.quitPlayers.has(leavingPlayer.playerId) == false) {
            this.onPlayerQuit(leavingPlayer);
        }
    }

    /**
     * Logic to handle player quits and when to disconnect the room
     * @param player The player who has quit the match
     */
    private onPlayerQuit(player: Player) {

        this.state.statusMessage = "";

        // Flag for if the room should disconnect after this player has quit
        let disconnectRoom: boolean = false;

        // Has the creator quit before a challenger has joined?
        if(this.metadata.team0 && this.metadata.team1 == null) {
            
            disconnectRoom = true;
        }

        // No other users are in the room so disconnect
        if(this.state.inProcessOfQuitingGame && this.state.quitPlayers.size >= 1 ) {
            
            disconnectRoom = true;
        }

        // Set team0 / team1 key on room's metadata
        this.setMetadata({
            [`team${player.playerId}`]: `${player.name} (Surrendered)`
        });

        // Set to true so when the other player joins after this one quits they'll be alerted in game and can then leave
        this.state.inProcessOfQuitingGame  = true;

        this.state.quitPlayers.set(player.playerId, player);

        // Should the room disconnect?
        if(disconnectRoom) {
            this.disconnect();
        }
    }

    onDispose() {
    }
}
