import { type, Schema, ArraySchema } from "@colyseus/schema";

export class World extends Schema {
    @type("number") width: number;
    @type("number") height: number;

    //
    // 1D representation of the grid (see https://softwareengineering.stackexchange.com/questions/212808/treating-a-1d-data-structure-as-2d-grid/212813#212813)
    // - @colyseus/schema does not support 2D arrays
    //
    @type(["number"]) grid: ArraySchema<number>; // 1D grid ()

    setGridValueAt(x: number, y: number, value: number) {
        const index = x + this.width * y;
        this.grid[index] = value;
    }

    getGridValueAt(x: number, y: number) {
        const index = x + this.width * y;
        return this.grid[index];
    }
}