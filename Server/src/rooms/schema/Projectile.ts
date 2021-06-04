import { Schema, type } from "@colyseus/schema";
import { GameRules } from "../tanks/rules";
import { Vector2 as Vector_2 } from "three";
import { Vector2 } from "./Vector2";
import logger from "../../helpers/logger";

export class Projectile extends Schema {
    
    // in-game properties
    @type("string") key: string;
    @type(Vector2) coords = new Vector2();

    playerId: number;

    vector2Helper: Vector_2 = new Vector_2();
    
    currentPathIndex: number = 0;
    projectilePath: Vector_2[] = null;
    onComplete: Function;

    constructor(playerId: number, path: Vector_2[]) {
        super();

        this.playerId = playerId;
        this.projectilePath = path;
        this.key = Date.now().toString();
    }

    vector2Position(): Vector_2 {
        this.vector2Helper.x = this.coords.x || 0;
        this.vector2Helper.y = this.coords.y || 0;

        return this.vector2Helper;
    }

    
    public updateProjectileAlongPath(deltaTime: number) {

        let currTargetPos: Vector_2 = this.projectilePath[this.currentPathIndex];

        let currPos: Vector_2 = this.vector2Position();

        let newPos = this.vector2Helper.lerpVectors(currPos, currTargetPos, GameRules.ProjectileSpeed * deltaTime);

        this.coords.assign({x: newPos.x, y: newPos.y});

        if(this.vector2Position().distanceTo(currTargetPos) < 0.05) {

            this.currentPathIndex++;
            
            if(this.currentPathIndex >= this.projectilePath.length) {

                if(this.onComplete) {
                    this.onComplete(this);
                }
            }
        }
    }

}
