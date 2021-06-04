import { Room, Client } from "colyseus";
import { TanksState, GameState } from "./schema/TanksState";
import { Player, PlayerReadyState } from "./schema/Player";
import { EnvironmentBuilder } from "./tanks/EnvironmentController";
import { GameRules } from "./tanks/rules";
import { Vector3, Vector2 as Vector_2 } from "three";
import logger from "../helpers/logger";
import { Vector2 } from "./schema/Vector2";
import { Projectile } from "./schema/Projectile";

export class TanksRoom extends Room<TanksState> {
    
    serverTime: number = 0;

    environmentController: EnvironmentBuilder; // Generates and maintains the game's terrain
    currPlayerMoveWait: number = 0; // Counter to help with the wait before allowing another action after the player has moved
    
    patchRate = 50; // The ms delay between room state patch updates

    currentPathIndex: number = 0;
    projectilePath: Vector_2[] = null;
    vector2Helper: Vector_2 = new Vector_2();

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
        this.resetForNewRound();

        this.registerMessageHandlers();

        // Set the Simulation Interval callback
        this.setSimulationInterval(dt => this.gameLoop(dt));
    }

    // Callback when a client has joined the room
    onJoin(client: Client, options: any) {
        console.info(`Client joined! - ${client.sessionId} ***`); console.log(options);

        //
        // FIXME: (assign playerId based on first "connected=false" player within state.players)
        //
        // If "creator" leaves the room while an opponent is still connected,
        // when a next player joins, he's going to have the same "playerId" as
        // the existing player.
        // 
        const username = options["joiningId"] || options["creatorId"];

        const isCreator = this.state.creatorId === username; // (this.clients.length === 1);

        logger.info(`*** On Join - Username = ${username} - Is Creator = ${isCreator} ***`);

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
            [`team${player.playerId}`]: client.sessionId
        });
    }

    /**
     * Run the game loop
     * @param deltaTime Delta time of the server
     */
    gameLoop(deltaTime: number) {
        this.serverTime += deltaTime;

        const deltaTimeSeconds = deltaTime / 1000;

        // Update the game state
        switch (this.state.gameState) {
            case GameState.SimulateRound:
                // Update the counter for player movement
                if (this.state.isPlayerMoving == true) {
                    this.currPlayerMoveWait += deltaTimeSeconds;

                    if (this.currPlayerMoveWait >= GameRules.MovementTime) {
                        this.state.isPlayerMoving = false;
                        this.currPlayerMoveWait = 0;
                        
                        // Check if the player has used up all their Action Points and end their turn if they have
                        if (this.state.getCurrentTurnPlayer().currentActionPoints <= 0) {
                            this.state.nextTurn();
                        }
                    }
                }
                else if(this.projectilePath) {
                    this.updateProjectileAlongPath(deltaTimeSeconds);
                }
                break;

            case GameState.EndRound:
                // Check if all the users are ready for a rematch
                let playersReady = this.checkForRematch();

                // Return out if not all of the players are ready yet.
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

    private updateProjectileAlongPath(deltaTime: number) {

        let projectile = this.state.getProjectile();

        if(projectile == null) {
            projectile = this.state.addNewProjectile();
        }

        let currPos: Vector_2 = projectile.position();

        let currTargetPos: Vector_2 = this.projectilePath[this.currentPathIndex];

        let newPos = this.vector2Helper.lerpVectors(currPos, currTargetPos, 25 * deltaTime);

        projectile.coords.assign({x: newPos.x, y: newPos.y});

        if(projectile.position().distanceTo(currTargetPos) < 0.05) {

            this.currentPathIndex++;
            
            if(this.currentPathIndex >= this.projectilePath.length) {

                logger.error(`*** Projectile go BOOM! ***`);

                this.state.removeProjectile();

                this.projectilePath = null;
                this.currentPathIndex = 0;
            }
        }
    }

    private registerMessageHandlers() {

        // Set the callback for the "ping" message for tracking server-client latency
        this.onMessage("ping", (client) => {
            client.send(0, { serverTime: this.serverTime });
        });

        // Set player as "ready"
        this.onMessage("ready", (client) => {
            const player = this.state.players[client.userData.playerId];
            if (player) {
                player.readyState = PlayerReadyState.READY;
            }
        });

        this.onMessage("skipTurn", (client, message) => {
            // Check if the player can do the action
            if (this.canDoAction(client.userData.playerId) == false) {
                return;
            }

            this.state.nextTurn();
        });

        this.onMessage("changeWeapon", (client: Client, message) => {
            // Check if the player can do the action
            if (this.canDoAction(client.userData.playerId) == false) {
                return;
            }

            this.state.switchPlayerWeapon(client.userData.playerId, message);
        });

        this.onMessage("setAimAngle", (client: Client, message) => {
            // Check if the player can do the action
            if (this.canDoAction(client.userData.playerId) == false) {
                return;
            }

            this.state.setAimAngle(client.userData.playerId, message);
        });

        this.onMessage("movePlayer", (client: Client, direction) => {
            // Skip if the player cannot do the action
            if (this.canDoAction(client.userData.playerId) === false) { return; }

            const player = this.state.players[client.userData.playerId];

            // Skip if movement is not possible
            if (!player.isMovementAllowed()) { return; }

            this.state.isPlayerMoving = true; // Update the player moving flag to true

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

        this.onMessage("fireWeapon", (client, message) => {

            // Check if the player can do the action
            if (this.canDoAction(client.userData.playerId) == false) {

                logger.info(`*** Player can't do action - ${client.userData.playerId} ***`);
                return;
            }

            const player = this.state.players[client.userData.playerId];

            // skip if the user does not have enough Action Points to fire
            if (player.currentActionPoints <= GameRules.FiringActionPointCost) { 
                
                logger.info(`*** Player does not have enough AP! - ${client.userData.playerId} ***`);
                return; 
            }

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

            console.log(`*** Fire Weapon - Barrel Forward =`, barrelForward, `  Barrel Pos = `, barrelPosition, `  Cannon Power = `, cannonPower, ` ***`);

            // Get the firepath using the barrel forward direction, barrel position, and the charged cannon power
            this.projectilePath = this.state.getFirePath(
                this.environmentController,
                new Vector3(barrelForward.x, barrelForward.y, barrelForward.z),
                new Vector3(barrelPosition.x, barrelPosition.y, barrelPosition.z),
                cannonPower
            );

            //logger.silly(`*** Got Fire Patch: `); console.log(this.projectilePath);

        });

        this.onMessage("getFirePath", (client, message) => {
            // Check if the player can do the action
            if (this.canDoAction(client.userData.playerId) == false) {
                return;
            }

            const player = this.state.players[client.userData.playerId];

            // skip if the user does not have enough Action Points to fire
            if (player.currentActionPoints <= GameRules.FiringActionPointCost) { return; }

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
            let firePath: Vector_2[] = this.state.getFirePath(
                this.environmentController,
                new Vector3(barrelForward.x, barrelForward.y, barrelForward.z),
                new Vector3(barrelPosition.x, barrelPosition.y, barrelPosition.z),
                cannonPower
            );

            // Get the player's currently active weapon
            const activeWeapon = this.state.getActiveWeapon(client.userData.playerId);

            // Send the path to the environment controller to check if any damage is done to terrain or player
            const damageData = this.environmentController.dealDamage(
                firePath[firePath.length - 1],
                activeWeapon.radius,
                activeWeapon.impactDamage
            );

            if (damageData) {
                damageData.updatedPlayers.forEach((updatedPlayer) => {
                    const player = this.state.players[updatedPlayer.playerId];

                    // Update player HP if there is damage
                    if (updatedPlayer.damage) {
                        player.hp -= updatedPlayer.damage;
                    }

                    if (player.hp <= 0) {
                        this.state.moveToState(GameState.EndRound);
                    }
                });
            }

            // Consume AP for firing weapon
            player.consumeActionPoints(GameRules.FiringActionPointCost);

            // TODO: is `damageData` required to be sent here??
            this.broadcast("receiveFirePath", { firePath, damageData });

            // Check if the player has used up all their Action Points and end their turn if they have
            if (player.currentActionPoints <= 0) {
                console.log(`*** Current player has used up all AP ***`);
                this.state.nextTurn();
            }
        });

        this.onMessage("quitGame", (client, message) => {
            // TODO CLIENT-SIDE:
            // Call room.leave() directly - onLeave() is going to be triggered with "consented" = true
        });
    }

    /**
     * Resets data for a new round of play
     */
    private resetForNewRound() {
        // Generate environment
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
        const notMoving = this.state.isPlayerMoving === false;
        const goodGameState = (this.state.gameState === GameState.SimulateRound);

        return (
            goodGameState && 
            playerId === this.state.currentTurn && // is current turn?
            notMoving
        );
    }

    /**
     * Checks if players want a rematch if they have a 'readyState' of "ready"
     */
    private checkForRematch() {
        let numPlayersReady: number = 0;

        this.state.players.forEach((player) => {
            if (player.readyState === PlayerReadyState.READY) {
                this.state.statusMessage = `${player.name} wants a rematch!`;
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

        this.setMetadata({
            [`team${leavingPlayer.playerId}`]: `${leavingPlayer.name} (Surrendered)`
        });
    }

    onDispose() {
    }
}
