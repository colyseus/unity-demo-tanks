import { Schema, type, MapSchema, ArraySchema } from "@colyseus/schema";
import { Vector3, Vector2 } from "three";
import logger from "../../helpers/logger";
import { EnvironmentBuilder } from "../tanks/EnvironmentController";

import { GameRulesSchema } from "./GameRules";
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
    @type(GameRulesSchema) gameRules: GameRulesSchema;
    @type([ Player ]) players = new ArraySchema<Player>();
    @type([Weapon]) weapons = new ArraySchema<Weapon>();
    @type(World) world = new World();
    @type({ map: Projectile }) projectiles = new MapSchema<Projectile>();

    @type("string") gameState: GameState;
    @type("string") previousGameState: GameState;

    @type("number") currentTurn: number; // either 0 or 1
    @type("number") turnNumber: number = 0; // incremental from 0 to X

    @type("string") statusMessage: string;

    isPlayerActing: boolean = false;
    isWaitingForProjectile: boolean = false;
    creatorId: string = "";
    environmentBuilder: EnvironmentBuilder;

    constructor() {
        super();

        this.gameRules = new GameRulesSchema().assign(GameRules);

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
        this.isPlayerActing = false;

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

    addNewProjectile(playerId: number, path: Vector2[]): Projectile {

        if(path == null || path.length == 0) {

            logger.error(`*** Cannot add projectile when there is no path! ***`);

            return;
        }

        const projectile = new Projectile(playerId, path);
        projectile.onComplete = () => {

            // Deal damage to the environment
            this.dealEnvironmentAndPlayerDamage(projectile);
            
            // Remove the projectile
            this.removeProjectile(projectile);

            this.isWaitingForProjectile = false;
        }

        projectile.coords.assign(path[0]);

        this.projectiles.set(projectile.key, projectile);

        return projectile;
    }

    private dealEnvironmentAndPlayerDamage(projectile: Projectile) {
        // Get the player's currently active weapon
        const activeWeapon = this.getActiveWeapon(projectile.playerId);

        // Send the impact position to the environment controller to check if any damage is done to terrain or player
        const damageData = this.environmentBuilder.dealDamage(
            projectile.projectilePath[projectile.projectilePath.length - 1],
            activeWeapon.radius,
            activeWeapon.impactDamage
        );

        if (damageData && damageData.updatedPlayers) {
            damageData.updatedPlayers.forEach((updatedPlayer) => {
                const player = this.players[updatedPlayer.playerId];

                // Update player HP if there is damage
                if (updatedPlayer.damage) {
                    player.hp -= updatedPlayer.damage;
                }

                if (player.hp <= 0) {
                    this.moveToState(GameState.EndRound);
                }
            });
        }
    }

    /**
     * Removes the projectile from the collection.
     * Currently assumes there will only ever be one projectile at a time in the collection.
     * Will cause the projectile on the client to explode
     * @returns 
     */
    removeProjectile(projectile: Projectile) {

        if(this.projectiles.has(projectile.key) == false) {
            
            logger.error(`*** No projectile with key ${projectile.key} ***`);
            return;
        }

        this.projectiles.delete(projectile.key);
    }

}
