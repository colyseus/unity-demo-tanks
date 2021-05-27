import { Vector2, Vector3 } from "three";
import { TanksState } from "../schema/TanksState";

const fastNoise = require("fastnoisejs");

interface Vector2Like {
    x: number;
    y: number;
}

export enum MapIconValues {
    EMPTY = 0,
    GROUND,
    PLAYER_1,
    PLAYER_2
}

export class EnvironmentBuilder
{
    // mapMatrix: number[][]; // 2D array to represent the terrain

    constructor (private state: TanksState) {}

    //Map Generation code
    //=======================================================================================================
    public GenerateEnvironment(width: number, height: number){
        this.state.world.width = width;
        this.state.world.height = height;

        // this.mapMatrix = new Array();

        let randomSeed: number = Math.random() * 50;
        let variation: number = 1.5;
        let noise = fastNoise.Create();
        noise.SetNoiseType(fastNoise.Perlin);
        noise.SetFrequency(10);

        for (let x = 0; x < width; ++x)
        {
            //Determine height using perlin noise
            let xSample: number = (x / width) * variation + randomSeed;
            let perlAmt: number = this.GetNoiseValue(xSample, randomSeed, noise);
            let noiseHeight: number = (height * perlAmt);
            //iterate from bottom to top of matrix, setting values based off of perlin noise amount
            //Add a new array for this column
            for (let y = 0; y < height; ++y) {
                this.state.world.setGridValueAt(x, y, (y < noiseHeight) ? MapIconValues.GROUND : MapIconValues.EMPTY);
            }
        }

        this.SetPlayerSpawns(height);
    }

    // Alias for world width/height
    get mapWidth () { return this.state.world.width; }
    get mapHeight () { return this.state.world.height; }

    private SetPlayerSpawns(height: number){
        let playerOnePlaced: boolean = false;
        let playerTwoPlaced: boolean = false;

        let oneX: number = 5;
        let twoX: number = this.state.world.width - 5;

        for (let y = 0; y < height; ++y)
        {
            if (this.state.world.getGridValueAt(oneX, y) === MapIconValues.EMPTY && !playerOnePlaced)
            {
                //We have nothing in this square
                this.state.world.setGridValueAt(oneX, y, MapIconValues.PLAYER_1);
                playerOnePlaced = true;
                this.SetPlayerPosition(0, new Vector2(oneX, y));
            }

            if (this.state.world.getGridValueAt(twoX, y) === MapIconValues.EMPTY && !playerTwoPlaced)
            {
                //We have nothing in this square
                this.state.world.setGridValueAt(twoX, y, MapIconValues.PLAYER_2);
                playerTwoPlaced = true;
                this.SetPlayerPosition(1, new Vector2(twoX, y));
            }
        }
    }

    private GetNoiseValue(xSample: number, y: number, noise: any): number {
        let noiseVal = noise.GetNoise(xSample, y);

        let convertedNoise = (noiseVal + 1) / 2;
        return convertedNoise;
    }

    //=======================================================================================================

    public GetPlayerPosition(playerNum:number): Vector2Like {
        return this.state.getPlayerByPlayerId(playerNum).coords;
    }

    public SetPlayerPosition(playerNum: number, coords: Vector2) {
        const player = this.state.getPlayerByPlayerId(playerNum);
        const previousPos = this.GetPlayerPosition(playerNum);

        if (previousPos) {
            this.state.world.setGridValueAt(previousPos.x, previousPos.y, MapIconValues.EMPTY);
        }

        player.coords.assign(coords);

        // Update the matrix
        this.state.world.setGridValueAt(coords.x, coords.y, (playerNum == 0)
            ? MapIconValues.PLAYER_1
            : MapIconValues.PLAYER_2);
    }

    public TrimFirePathToEnvironment(origPath: Vector3[]): Vector3[] {
        let updatedPath: Vector3[] = [];
        updatedPath.push(origPath[0].clone());
        let foundTermination: boolean = false;
        for (let i = 0; i < origPath.length; ++i)
        {
            if(foundTermination)
                break;
            
            //console.log(`*** Orig Path ${i} - (${origPath[i].x}, ${origPath[i].y})`);

            let coords: Vector2 = this.ClientPositionToMapCoordinates(origPath[i]);
            if (coords == null)
            {
                //We're shooting over the map, keep the position
                updatedPath.push(origPath[i].clone());
            }
            else
            {
                try
                {
                    //Check to see what is here
                    switch (this.state.world.getGridValueAt(coords.x, coords.y))
                    {
                        case MapIconValues.EMPTY:
                            updatedPath.push(origPath[i].clone());
                            break;
                        case MapIconValues.GROUND:
                            foundTermination = true;
                            //Create a new position thats 0.5 up from the ground block
                            let pos: Vector3 = new Vector3(coords.x, coords.y, 0);
                            pos.x = origPath[i].x;
                            pos.y += 0.5;
                            updatedPath.push(pos);
                            break;
                    }
                }
                catch (err)
                {
                    console.error(`Error getting map coordinate: ${coords.x}, ${coords.y} - Orig Path: ${origPath[i].x}, ${origPath[i].y}`);
                    throw `Error getting map coordinate: ${coords.x}, ${coords.y}`;
                }
                
            }
        }

        return updatedPath;// new CannonController.CannonFirePath(updatedPath.ToArray());
    }

    public ClientPositionToMapCoordinates(clientPos: Vector3) : Vector2
    {
        let localPos: Vector3 = clientPos.clone();
        localPos.y = this.ClampNumber(localPos.y, 0.0, Number.MAX_VALUE);
        let coords: Vector2 = new Vector2(Math.round(localPos.x), Math.round(localPos.y));
        if (coords.y >= this.mapHeight)
        {
            return null;    //We're shooting over the top
        }

        if (coords.x < 0 || coords.x >= this.mapWidth)
        {
            return null;
        }

        return coords;
    }

    private ClampNumber(value: number, min: number, max: number) : number {
        if(value < min) {
            value = min;
        }
        else if (value > max) {
            value = max;
        }

        return value;
    }

    public GetAvailableSpace(direction: number, startPosition: Vector2Like): Vector2Like {
        let newX: number = startPosition.x + direction;
        let newY: number = startPosition.y;

        if (newX < 0 || newX >= this.state.world.width)
        {
            //Cant go off the right or left of the map, do nothing?
            return startPosition;
        }
        
        let mapValue = this.state.world.getGridValueAt(newX, newY);
        if (mapValue === MapIconValues.EMPTY)
        {
            newY = this.FindNextSafeGridPos(newX, newY);
        }
        else if (mapValue === MapIconValues.GROUND)
        {
            //Don't pass the top of the map
            let yVal = Math.min(newY + 1, this.state.world.height);
            let mapVal = this.state.world.getGridValueAt(newX, yVal);
            if(mapVal === MapIconValues.EMPTY)
            {
                newY = yVal;
            }
            else
            {
                return startPosition;
            }
            
            //Enable if we want to be able to move up any amount of blocks - Stolen from Unity project
            //Theres a ground block in this space, check Up until we find an empty one
            //bool emptyFound = false;
            //int yVal = Mathf.Min(newY + 1, mapMatrix.GetLength(1) - 1);
            //while (!emptyFound)
            //{
            //    int mapVal = mapMatrix[newX, yVal];
            //    if (mapVal == (int)eMapItem.EMPTY)
            //    {
            //        newY = yVal;
            //        emptyFound = true;
            //    }
            //    else
            //    {
            //        yVal++;
            //    }
            //}
        }
        else
        {
            //Must be another player here, no movement!
            return startPosition;
        }

        return { x: newX, y: newY };
    }

    //Get the next grid Y coordinate we can have a tank at
    private FindNextSafeGridPos(startX: number, startY: number) : number {
        let groundFound: boolean = false;
        let yVal: number = Math.max(startY - 1, 0);

        while(!groundFound)
        {
            let mapVal = this.state.world.getGridValueAt(startX, yVal);
            if(mapVal === MapIconValues.EMPTY)
            {
                //This space is empty, check lower
                startY = yVal;
                if (yVal == 0)
                {
                    //We've hit bedrock
                    groundFound = true;
                }
                else
                {
                    --yVal;
                }
            }
            else{
                groundFound = true;
            }
        }

        return startY;
    }

    public dealDamage(localPosition: Vector3, radius: number, damage: number) {
        let coords: Vector2 = this.localPositionToMapCoordinates(localPosition);
        if (coords == null)
        {
            console.error("Explosion did not take place within the map matrix!");
            return;
        }

        const updatedPlayersMap = new Map<number, { playerId: number, damage?: number }>();

        let impactedCoordinates: Vector2[] = this.getImpactedCoordinatesList(coords, radius);
        for (let i: number = 0; i < impactedCoordinates.length; ++i)
        {
            let damagedPlayers: number[] = this.dealDamageToCoordinates(impactedCoordinates[i]);

            if(damagedPlayers && damagedPlayers.length > 0) {
                for(let j = 0; j < damagedPlayers.length; j++) {
                    updatedPlayersMap.set(damagedPlayers[j], { playerId: damagedPlayers[j], damage });
                }
            }
        }

        // Get the player's with updated positions
        let updatedPlayersPositions: any[] =  this.updateGrid();

        if(updatedPlayersPositions && updatedPlayersPositions.length > 0) {
            for(let i = 0; i < updatedPlayersPositions.length; i++) {

                this.SetPlayerPosition(updatedPlayersPositions[i].playerId, updatedPlayersPositions[i].playerPos);

                if(updatedPlayersMap.has(updatedPlayersPositions[i].playerId)) {
                    // let playerObj = updatedPlayersMap.get(updatedPlayersPositions[i].playerId);

                    // playerObj.playerPos = updatedPlayersPositions[i].playerPos;
                }
                else {
                    updatedPlayersMap.set(updatedPlayersPositions[i].playerId, {
                        playerId: updatedPlayersPositions[i].playerId,
                        //  playerPos: updatedPlayersPositions[i].playerPos
                    });
                }
            }
        }

        
        const updatedPlayers = Array.from(updatedPlayersMap.values());

        return { impactedCoordinates, updatedPlayers };
    }

    private dealDamageToCoordinates(coords: Vector2): number[]
    {
        let damagedPlayers: number[];

        //Get what is at this position
        let mapItem: number = this.state.world.getGridValueAt(coords.x, coords.y);
        switch (mapItem)
        {
            case MapIconValues.GROUND:
            {
                //Ground goes boom?
                this.state.world.setGridValueAt(coords.x, coords.y, MapIconValues.EMPTY);

                break;
            }
            case MapIconValues.PLAYER_1:
            case MapIconValues.PLAYER_2:
            {
                if(damagedPlayers == null) {
                    damagedPlayers = [];
                }

                if(mapItem == MapIconValues.PLAYER_1) {
                    damagedPlayers.push(0);
                }
                else {
                    damagedPlayers.push(1);
                }

                break;
            }
        }

        return damagedPlayers;
    }

    private updateGrid(): any[]
    {
        let updatedPlayers: any[] = [];

        //Check player positions, if any are floating, they need to be moved down
        for (let x = 0; x < this.mapWidth; ++x)
        {
            for (let y = 0; y < this.mapHeight; ++y)
            {
                let mapValue: number = this.state.world.getGridValueAt(x, y);
                switch (mapValue)
                {
                    case MapIconValues.PLAYER_1:
                    case MapIconValues.PLAYER_2:
                    {
                        let playerId: number = mapValue == MapIconValues.PLAYER_1 ? 0 : 1;
                        let newY: number = this.FindNextSafeGridPos(x, y);

                        if(newY != y) {

                            let playerPos: Vector2 = new Vector2(x, newY);
                            this.updatePlayerPos(mapValue === MapIconValues.PLAYER_1, new Vector2(x,y), playerPos);
    
                            updatedPlayers.push({ playerId, playerPos });
                        }

                        break;
                    }
                }
            }
        }

        return updatedPlayers;
    }

    private getImpactedCoordinatesList(coords: Vector2, radius: number): Vector2[]
    {
        let impactedPoints: Vector2[] = [];
        let origX: number = coords.x;
        let origY: number = coords.y;

        let left: number = 0;
        let right: number = 0;
        let up: number = 0;
        let down: number = 0;

        //0 is the origin, start at 1 and go to radius
        for (let i = 1; i <= radius; ++i)
        {
            left = origX - i;
            right = origX + i;
            up = origY + i;
            down = origY - i;

            //Iterate from left to right
            for (let x = left; x <= right; ++x)
            {
                //Check if we're within the horizontal bounds of the map
                if (x >= 0 && x < this.mapWidth)
                {
                    //Iterate from bottom to top
                    for (let y = down; y <= up; ++y)
                    {
                        //Check if we're within the vertical bounds of the map
                        if (y >= 0 && y < this.mapHeight)
                        {
                            let pos: Vector2 = new Vector2(x, y);
                            if (!impactedPoints.includes(pos))
                            {
                                impactedPoints.push(pos);
                            }
                        }
                    }
                }
            }
        }

        return impactedPoints;
    }

    public localPositionToMapCoordinates(localPos: Vector3): Vector2
    {
        localPos.y = this.ClampNumber(localPos.y, 0.0, Number.MAX_VALUE);
        let coords: Vector2 = new Vector2(Math.round(localPos.x), Math.round(localPos.y));
        if (coords.y >= this.mapHeight)
        {
            return null;    //We're shooting over the top
        }

        if (coords.x < 0 || coords.x >= this.mapWidth)
        {
            return null;
        }

        return coords;
    }

    private updatePlayerPos(playerOne: boolean, previousCoordinates: Vector2, newCoordinates: Vector2)
    {
        // Set the player's old spot to empty
        this.state.world.setGridValueAt(previousCoordinates.x, previousCoordinates.y, MapIconValues.EMPTY);
        // Set the player's new spot to have the player
        this.state.world.setGridValueAt(newCoordinates.x, newCoordinates.y, playerOne ? MapIconValues.PLAYER_1 : MapIconValues.PLAYER_2);
    }
}