import { Schema, type, MapSchema, ArraySchema } from "@colyseus/schema";
import { Vector3, Vector2 } from "three";
import logger from "../../helpers/logger";
import { EnvironmentBuilder } from "../tanks/EnvironmentController";

import { GameRules, weaponList } from "../tanks/rules";
import { Player, PlayerReadyState } from "./Player";
import { Projectile } from "./Projectile";
import { Weapon } from "./Weapon";
import { World } from "./World";

export enum GameState {
    None = "None",
    Waiting = "Waiting",
    BeginRound = "BeginRound",
    SimulateRound = "SimulateRound",
    EndRound = "EndRound"
}

export class TanksState extends Schema {
    @type([ Player ]) players = new ArraySchema<Player>();
    @type([Weapon]) weapons = new ArraySchema<Weapon>();
    @type(World) world = new World();
    @type([Projectile]) projectiles = new ArraySchema<Projectile>();

    @type("string") gameState: GameState;
    @type("string") previousGameState: GameState;

    @type("number") currentTurn: number; // either 0 or 1
    @type("number") turnNumber: number = 0; // incremental from 0 to X

    @type("string") statusMessage: string;

    isPlayerMoving: boolean = false;
    creatorId: string = "";

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

    restart() {

        this.currentTurn = 0;
        this.turnNumber = 0;
        this.statusMessage = "";
        this.isPlayerMoving = false;

        // Reset players
        this.players.forEach((player) => {
            player.hp = GameRules.MaxHitPoints;
            player.readyState = PlayerReadyState.WAITING;
            player.currentWeapon = 0;
            player.resetActions();
        });
    }

    /**
     * Go to next turn.
     */
    nextTurn() {
        this.turnNumber++;

        this.currentTurn = this.currentTurn == 0 ? 1 : 0;

        // reset player actions
        this.players.forEach((player) => player.resetActions());
    }

    /**
     * Move the room game state to the new state
     * @param {GameState} newState The new state to move to
     */
    moveToState (nextGameState: GameState) {
        this.previousGameState = this.gameState;
        this.gameState = nextGameState;
    }

    switchPlayerWeapon(playerId: number, weaponIndex: number) {
        const player = this.players[playerId];
        if (
            player && 
            weaponIndex >= 0 && // validate weapon index
            weaponIndex < this.weapons.length
        ) {
            player.currentWeapon = weaponIndex;
        }
    }

    setAimAngle(playerId: number, aimAngle: number) {
        const player = this.players[playerId];

        player.aimAngle = aimAngle;
    }

    getActiveWeapon(playerId: number) {
        const player = this.players[playerId];
        return this.weapons[player.currentWeapon] || this.weapons[0]; // fallback to weapons[0] 
    }

    getCurrentTurnPlayer(): Player {
        return this.players[this.currentTurn];
    }

    // FIXME: this method could be extracted inside Environment class
    getFirePath(environment: EnvironmentBuilder, barrelForward: Vector3, barrelPosition: Vector3, cannonPower: number): Vector2[] {
        let initialVelocity: Vector3 = barrelForward.clone().multiplyScalar(cannonPower);
        let currentVelocity: Vector2 = new Vector2(initialVelocity.x, initialVelocity.y);
        let currPos: Vector2 = new Vector2(barrelPosition.x, barrelPosition.y);// barrelPosition.clone();
        let pathSteps: Vector2[] = [];
        pathSteps.push(currPos.clone());
        const grav: number = -0.98;
        while (currPos.y > -1.0) {
            currentVelocity.y += grav;
            currPos.add(currentVelocity);
            pathSteps.push(currPos.clone());
        }

        return this.getHighAccuracyFirePath(environment, pathSteps);
    }

    // FIXME: this method could be extracted inside Environment class
    getHighAccuracyFirePath(environment: EnvironmentBuilder, originalPath: Vector2[]) {
        let newPath: Vector2[] = [];
        let previousPos: Vector2 = originalPath[0].clone();

        newPath.push(previousPos.clone());
        for (let i = 1; i < originalPath.length; ++i) {
            let currPathSeg = originalPath[i].clone();
            let dist: number = Math.floor(previousPos.distanceTo(currPathSeg)) * 2;
            let stepSize: Vector2 = new Vector2().subVectors(currPathSeg, previousPos).divideScalar(dist);

            for (let j = 0; j < dist; ++j) {
                previousPos.add(stepSize);
                newPath.push(previousPos.clone());
            }
        }

        return environment.TrimFirePathToEnvironment(newPath);
    }

    addNewProjectile(): Projectile {

        const projectile = new Projectile();

        this.projectiles.push(projectile);

        return projectile;
    }

    /**
     * Removes the projectile from the collection.
     * Currently assumes there will only ever be one projectile at a time in the collection.
     * Will cause the projectile on the client to explode
     * @returns 
     */
    removeProjectile() {

        if(this.projectiles.length == 0) {
            
            logger.error(`*** No projectile to remove? ***`);
            return;
        }

        this.projectiles.shift();
    }

    /**
     * Returns the first projectile in the collection.
     * Currently assumes there will only ever be one projectile at a time in the collection.
     * @returns 
     */
    getProjectile(): Projectile {

        if(this.projectiles.length == 0) {
            return null;
        }

        return this.projectiles[0];
    }
}
