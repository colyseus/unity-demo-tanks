import { Schema, type } from "@colyseus/schema";
import { GameRules } from "../tanks/rules";
import { Vector2 as Vector_2 } from "three";
import { Vector2 } from "./Vector2";

export class Projectile extends Schema {
    
    // in-game properties
    @type(Vector2) coords = new Vector2();

    vector2Helper: Vector_2 = new Vector_2();

    position(): Vector_2 {
        this.vector2Helper.x = this.coords.x || 0;
        this.vector2Helper.y = this.coords.y || 0;

        return this.vector2Helper;
    }
}
