import { type, Schema, ArraySchema } from "@colyseus/schema";

export class World extends Schema {
    @type("number") width: number;
    @type("number") height: number;
    @type(["number"]) grid = new ArraySchema<number>(); // 1D grid (https://softwareengineering.stackexchange.com/questions/212808/treating-a-1d-data-structure-as-2d-grid/212813#212813)
}