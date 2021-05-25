import { Room, Client, generateId } from "colyseus";
import { TanksState, GameState } from "./schema/TanksState";
import { Player, PlayerReadyState } from "./schema/Player";
import { EnvironmentBuilder } from "./customLogic/EnvironmentController";
import { GameRules } from "./customLogic/gameRules";
import { Vector2, Vector3 } from "three";
import * as tanks from "./customLogic/tanks";

export class TanksRoom extends Room<TanksState> {
    
    serverTime: number = 0;

    environmentController: EnvironmentBuilder; // Generates and maintains the game's terrain
    currPlayerMoveWait: number = 0; // Counter to help with the wait before allowing another action after the player has moved

    isPaused: boolean = true;

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

        // Initialize initial room state
        this.setState(new TanksState().assign({
            gameState: GameState.SimulateRound,
            previousGameState: GameState.None,
        }));

        this.resetForNewRound();

        // Set the callback for the "ping" message for tracking server-client latency
        this.onMessage("ping", (client) => {
            client.send(0, { serverTime: this.serverTime });
        });

        this.onMessage("skipTurn", (client, message) => {
            // Check if the player can do the action
            if (this.canDoAction(client.sessionId) == false) {
                return;
            }

            this.state.nextTurn();
        });

        this.onMessage("changeWeapon", (client, message) => {
            // Check if the player can do the action
            if (this.canDoAction(client.sessionId) == false) {
                return;
            }

            this.state.switchPlayerWeapon(client.sessionId, message);
        });

        this.onMessage("movePlayer", (client, direction) => {
            // Skip if the player cannot do the action
            if (this.canDoAction(client.sessionId) === false) { return; }

            const player = this.state.players.get(client.sessionId);

            // Skip if movement is not possible
            if (!player.isMovementAllowed()) { return; }

            this.state.isPlayerMoving = true; // Update the player moving flag to true

            const nextPosition = this.environmentController.GetAvailableSpace(direction, player.coords);
            const hasMoved: boolean = player.coords.x !== nextPosition.x;

            // Attempt to move the player
            if (hasMoved) {
                // update player coords
                player.coords.assign(nextPosition);

                // Consume AP
                player.consumeActionPoints(GameRules.MovementActionPointCost);

                // Check if the player has used up all their Action Points and end their turn if they have
                if (player.currentActionPoints <= 0) {
                    this.state.nextTurn();
                }
            }
        });

        this.onMessage("getFirePath", (client, message) => {
            // Check if the player can do the action
            if (this.canDoAction(client.sessionId) == false) {
                return;
            }

            const player = this.state.players.get(client.sessionId);

            // skip if the user does not have enough Action Points to fire
            if (player.currentActionPoints <= GameRules.FiringActionPointCost) { return; }

            //0 - player number 0 = 1, 1 = 2
            //1 - moveDirection
            const param = request.param;

            // 0 = barrel forward | 1 = barrel position | 2 = cannon power
            if (param == null || param.length < 3) {
                throw "Error - Get Fire Path - Missing parameter";
                return;
            }

            const playerNum = this.currentTurnContainer.playerTurn;

            // Get the firepath using the barrel forward direction, barrel position, and the charged cannon power
            let firePath: Vector3[] = getFirePath(this, new Vector3(Number(param[0].x), Number(param[0].y), Number(param[0].z)), new Vector3(Number(param[1].x), Number(param[1].y), Number(param[1].z)), Number(param[2]));

            // Get the player's currently active weapon
            const activeWeapon: Weapon = this.weaponController.getPlayerActiveWeapon(playerNum);

            // Send the path to the environment controller to check if any damage is done to terrain or player
            let damageData: any = this.environmentController.dealDamage(firePath[firePath.length - 1], activeWeapon.radius, activeWeapon.impactDamage);

            if (damageData) {
                let updatedPlayers: any[] = damageData.updatedPlayers;

                // Update player hit points
                if (updatedPlayers) {
                    for (let i = 0; i < updatedPlayers.length; i++) {
                        let playerId = updatedPlayers[i].playerId;
                        if (this.playerHP.has(playerId)) {
                            // Update player HP if there is damage
                            if (updatedPlayers[i].damage) {
                                let currentHP = this.playerHP.get(playerId);
                                this.playerHP.set(playerId, currentHP - updatedPlayers[i].damage);
                            }

                            updatedPlayers[i].remainingHP = this.playerHP.get(playerId);
                        }
                    }
                }

            }

            // Check player hit points
            this.playerHP.forEach((hp, playerId) => {
                if (hp <= 0) {
                    // Player has been destroyed!
                    moveToState(this, GameState.EndRound);
                }
            });

            // Consume AP for firing weapon
            player.consumeActionPoints(GameRules.FiringActionPointCost);

            this.broadcast("receiveFirePath", {
                firePath,
                remainingAP: this.currentTurnContainer.currentAP,
                playerNumber: playerNum,
                damageData,
            });

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

        // Set the Simulation Interval callback
        this.setSimulationInterval(dt => this.gameLoop(dt));
    }

    // Callback when a client has joined the room
    onJoin(client: Client, options: any) {
        console.info(`Client joined! - ${client.sessionId} ***`);

        const isCreator = (this.state.players.size === 0);

        const newPlayer = new Player().assign({
            sessionId: client.sessionId,
            name: options.displayName,
            teamId: (isCreator) ? 0 : 1,
        });

        // Set player instance to be synchronized
        this.state.players.set(client.sessionId, newPlayer);

        // Set team0 / team1 key on room's metadata
        this.setMetadata({
            [`team${newPlayer.teamId}`]: client.sessionId
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
                    }
                }
                break;

            case GameState.EndRound:
                // Check if all the users are ready for a rematch
                let playersReady = this.checkForRematch();

                // Return out if not all of the players are ready yet.
                if (playersReady == false) {
                    return;
                }

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
     * Resets data for a new round of play
     */
    private resetForNewRound() {
        // Generate environment
        this.environmentController.GenerateEnvironment(50, 10);

        // Reset turn data
        this.currentTurnContainer.completeReset();

        // Reset players
        this.state.players.forEach((player) => {
            player.hp = GameRules.MaxHitPoints;
            player.readyState = PlayerReadyState.WAITING;
            player.currentWeapon = 0;
        });
    }

    /**
     * Checks if the game is in the simulate round state and if it's the player's turn
     * @param sessionId Session Id of the player we're checking
     * @returns 
     */
    private canDoAction(sessionId: string): boolean {
        const notMoving = this.state.isPlayerMoving === false;
        const goodGameState = (this.state.gameState === GameState.SimulateRound);

        return goodGameState && this.isPlayersTurn(sessionId) && notMoving;
    }

    /**
     * Checks if it is a player's turn using their session Id
     * @param sessionId Session Id of the player we want to check for
     * @returns True if it is the player's turn
     */
    private isPlayersTurn(sessionId: string): boolean {
        let playerNum = this.state.currentTurn;
        let playerTurnId = this.state.players.get(sessionId).teamId;
        return playerNum == playerTurnId;
    }

    /**
     * Checks if players want a rematch if they have a 'readyState' of "ready"
     */
    private checkForRematch() {
        const players = Array.from(this.state.players.values())
        const playersReady = players.filter((player) => player.readyState === PlayerReadyState.READY);

        this.state.players.forEach((player) => {

        });

        if (userArr.length <= 0)
            playersReady = false;

        for (let user of userArr) {

            let readyState: string = user.readyState;;

            if (readyState == null || readyState != "ready") {
                playersReady = false;
            }
            else {

                let playerId: number = this.players.get(user.sessionId);

                let playerName = "Player";
                if (playerId == 0) {
                    playerName = this.metadata.team0;
                }
                else {
                    playerName = this.metadata.team1;
                }

                setRoomAttribute(this, GeneralMessage, `${playerName} wants a rematch!`);
            }
        }

        return (playersReady.length === 2);
    }

    // Callback when a client has left the room
    async onLeave(client: Client, consented: boolean) {
        const leavingPlayer = this.state.players.get(client.sessionId);
        if (!leavingPlayer) {
            console.error(`*** onLeave - No Player for sessionId - ${client.sessionId} ***`);
            return;
        }

        // Sync player as not connected.
        leavingPlayer.connected = false;

        this.setMetadata({
            [`team${leavingPlayer.teamId}`]: `${leavingPlayer.name} (Surrendered)`
        });
    }

    onDispose() {
    }
}
