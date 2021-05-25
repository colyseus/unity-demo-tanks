import { Schema, type } from "@colyseus/schema";
import { GameRules } from "../customLogic/gameRules";
import { Vector2 } from "./Vector2";

export enum PlayerReadyState {
    WAITING = "waiting",
    READY = "ready",
}

export class Player extends Schema {
    @type("string") sessionId: string;
    @type("string") readyState: PlayerReadyState;

    @type("number") teamId: number;
    @type("string") name: string;

    // in-game properties
    @type("number") hp: number;
    @type(Vector2) coords = new Vector2();
    @type("number") currentWeapon: number = 0;

    // movement
    @type("number") currentMovement: number;
    @type("number") currentActionPoints: number;

    // meta
    @type("number") timestamp: number;
    @type("boolean") connected: boolean;

    resetActions() {
        this.currentMovement = 0;
        this.currentActionPoints = GameRules.MaxActionPoints;
    }

    consumeActionPoints(amount: number) {
        this.currentActionPoints -= amount;
    }

    isMovementAllowed() {
        return (
            this.currentMovement < GameRules.MaxMovement && 
            this.currentActionPoints >= GameRules.MovementActionPointCost
        )
    }
}
