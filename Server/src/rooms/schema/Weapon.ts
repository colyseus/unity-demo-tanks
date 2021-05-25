import { Schema, type } from "@colyseus/schema";

export class Weapon extends Schema {
    @type("string") name: string;
    @type("number") maxCharge: number;
    @type("number") chargeTime: number;
    @type("number") radius: number;
    @type("number") impactDamage: number;
}
