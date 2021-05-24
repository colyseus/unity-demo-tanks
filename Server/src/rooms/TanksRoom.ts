import { Room, Client, generateId } from "colyseus";
import { TanksState, Player, GameState, PlayerReadyState } from "./schema/TanksState";
import { EnvironmentBuilder } from "./customLogic/EnvironmentController";
import { TurnContainer } from "./customLogic/turnModel";
import { GameRules } from "./customLogic/gameRules";
import * as tanks from "./customLogic/tanks";

export class TanksRoom extends Room<TanksState> {
    
    serverTime: number = 0;

    environmentController: EnvironmentBuilder; // Generates and maintains the game's terrain
    currentTurnContainer: TurnContainer; // Helps keep track of the current turn
    playerMoving: boolean = false; // Flag to track when a player has moved as an action
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

            this.endCurrentTurn(client, true);
        });

        this.onMessage("quitGame", (client, message) => {
            // TODO CLIENT-SIDE:
            // Call room.leave() directly - onLeave() is going to be triggered with "consented" = true
        });

        // Set the callback for the "customMethod" message
        this.onMessage("customMethod", (client, request) => {
            try {
                tanks.ProcessMethod(this, client, request);

            } catch (error) {
                console.error("Error with custom Method logic: " + error);
            }
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
                if (this.playerMoving == true) {
                    this.currPlayerMoveWait += deltaTimeSeconds;

                    if (this.currPlayerMoveWait >= GameRules.MovementTime) {
                        this.playerMoving = false;
                        this.currPlayerMoveWait = 0;
                    }
                }
                break;

            case GameState.EndRound:
                // Check if all the users are ready for a rematch
                let playersReady = checkForRematch(this, this.state.players);

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
        const notMoving = this.playerMoving == false;
        const goodGameState = (this.state.gameState === GameState.SimulateRound);
        const goodPlayer = this.players.has(sessionId) && this.isPlayersTurn(sessionId);

        return goodGameState && goodPlayer && notMoving;
    }

    /**
     * Checks if it is a player's turn using their session Id
     * @param sessionId Session Id of the player we want to check for
     * @returns True if it is the player's turn
     */
    private isPlayersTurn(sessionId: string): boolean {
        let playerNum = this.currentTurnContainer.playerTurn;
        let playerTurnId = this.state.players.get(sessionId).teamId;
        return playerNum == playerTurnId;
    }


    /**
     * Ends the current player's turn
     * @param client The client for whose turn is ending
     * @param isSkip Are we ending the turn because the player has skipped?
     */
    private endCurrentTurn(client: Client, isSkip: boolean = false) {
        if (this.currentTurnContainer == null) {
            this.currentTurnContainer = new TurnContainer();
        }

        // Increment the current turn 
        this.currentTurnContainer.incrementTurnCount();

        this.state.currentTurn = this.currentTurnContainer.turnNumber;

        // Let everyone know this turn has ended
        this.broadcast("turnComplete", { turnData: JSON.stringify(roomRef.currentTurnContainer), wasSkip: isSkip });

        this.currentTurnContainer.resetActions();
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
