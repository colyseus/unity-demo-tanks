import { Schema, type, MapSchema, ArraySchema } from "@colyseus/schema";
import { weaponList } from "../customLogic/gameRules";

export enum PlayerReadyState {
    WAITING = "waiting",
    READY = "ready",
}

export enum GameState {
    None = "None",
    Waiting = "Waiting",
    BeginRound = "BeginRound",
    SimulateRound = "SimulateRound",
    EndRound = "EndRound"
}

export class Player extends Schema {
    @type("string") sessionId: string;
    @type("string") readyState: PlayerReadyState; 

    @type("number") teamId: number;
    @type("string") name: string;
    @type("number") hp: number;
    @type("number") currentWeapon: number = 0;

    @type("number") timestamp: number;
    @type("boolean") connected: boolean;
}

export class Weapon extends Schema {
    @type("string") name: string;
    @type("number") maxCharge: number;
    @type("number") chargeTime: number;
    @type("number") radius: number;
    @type("number") impactDamage: number;
}

export class TanksState extends Schema {
    @type({ map: Player }) players = new MapSchema<Player>();
    @type(["number"]) worldMap = new ArraySchema<number>();
    @type([Weapon]) weapons = new ArraySchema<Weapon>();

    @type("string") gameState: GameState;
    @type("string") previousGameState: GameState;

    @type("number") currentTurn: number;
    @type("string") statusMessage: string;

    constructor() {
        super();

        // initialize all weapons for synchornization
        for (let i = 0; i < weaponList.length; i++) {
            // initialize weapon 
            const weapon = new Weapon().assign(weaponList[i]);

            // push weapon to weapons array
            this.weapons.push(weapon);
        }
    }

    /**
     * Move the room game state to the new state
     * @param {GameState} newState The new state to move to
     */
    moveToState (nextGameState: GameState) {
        this.previousGameState = this.gameState;
        this.gameState = nextGameState;
    }

    switchPlayerWeapon(sessionId: string, weaponIndex: number) {
        const player = this.players.get(sessionId);
        if (
            player && 
            weaponIndex >= 0 && // validate weapon index
            weaponIndex < this.weapons.length
        ) {
            player.currentWeapon = weaponIndex;
        }
    }
}
