import { Schema, type } from "@colyseus/schema";

export class GameRulesSchema extends Schema {

    @type("int32") MaxActionPoints: number;
    @type("int32") MovementActionPointCost: number;
    @type("int32") FiringActionPointCost: number;
    @type("int32") ProjectileSpeed: number;
    @type("int32") MaxHitPoints: number;
    @type("int32") MovementTime: number;
}