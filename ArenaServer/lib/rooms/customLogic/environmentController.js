"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.EnvironmentBuilder = void 0;
const logger = require("../../helpers/logger");
const utilities = require("../../helpers/LSUtilities");
const fastNoise = require("fastnoisejs");
const three_1 = require("three");
const MapIconValues = {
    EMPTY: 0,
    GROUND: 1,
    PLAYER_1: 2,
    PLAYER_2: 3
};
class EnvironmentBuilder {
    //Map Generation code
    //=======================================================================================================
    GenerateEnvironment(width, height) {
        this.mapWidth = width;
        this.mapHeight = height;
        this.mapMatrix = new Array();
        let randomSeed = utilities.getRandomFloatInclusive(0, 50);
        let variation = 1.5;
        let noise = fastNoise.Create();
        noise.SetNoiseType(fastNoise.Perlin);
        noise.SetFrequency(10);
        for (let x = 0; x < width; ++x) {
            //Determine height using perlin noise
            let xSample = (x / width) * variation + randomSeed;
            let perlAmt = this.GetNoiseValue(xSample, randomSeed, noise);
            let noiseHeight = (height * perlAmt);
            //iterate from bottom to top of matrix, setting values based off of perlin noise amount
            //Add a new array for this column
            let column = new Array();
            for (let y = 0; y < height; ++y) {
                column.push(y < noiseHeight ? MapIconValues.GROUND : MapIconValues.EMPTY);
            }
            //Add the column to the matrix
            this.mapMatrix.push(column);
        }
        this.SetPlayerSpawns(height);
    }
    SetPlayerSpawns(height) {
        let playerOnePlaced = false;
        let playerTwoPlaced = false;
        let oneX = 5;
        let twoX = this.mapMatrix.length - 5;
        this.playerCoordinates = new Map();
        for (let y = 0; y < height; ++y) {
            if (this.mapMatrix[oneX][y] == MapIconValues.EMPTY && !playerOnePlaced) {
                //We have nothing in this square
                this.mapMatrix[oneX][y] = MapIconValues.PLAYER_1;
                playerOnePlaced = true;
                this.SetPlayerPosition(0, new three_1.Vector2(oneX, y));
            }
            if (this.mapMatrix[twoX][y] == MapIconValues.EMPTY && !playerTwoPlaced) {
                //We have nothing in this square
                this.mapMatrix[twoX][y] = MapIconValues.PLAYER_2;
                playerTwoPlaced = true;
                this.SetPlayerPosition(1, new three_1.Vector2(twoX, y));
            }
        }
    }
    GetNoiseValue(xSample, y, noise) {
        let noiseVal = noise.GetNoise(xSample, y);
        let convertedNoise = (noiseVal + 1) / 2;
        return convertedNoise;
    }
    //=======================================================================================================
    MovePlayer(player, direction) {
        let startPos = this.GetPlayerPosition(player);
        let endPos = this.GetAvailableSpace(direction, startPos);
        let moved = startPos.x != endPos.x;
        if (moved) {
            this.SetPlayerPosition(player, endPos);
        }
        return moved;
    }
    GetPlayerPosition(player) {
        return this.playerCoordinates.get(player);
    }
    SetPlayerPosition(player, coords) {
        const previousPos = this.GetPlayerPosition(player);
        if (previousPos) {
            this.mapMatrix[previousPos.x][previousPos.y] = MapIconValues.EMPTY;
        }
        this.playerCoordinates.set(player, coords);
        // Update the matrix
        this.mapMatrix[coords.x][coords.y] = player == 0 ? MapIconValues.PLAYER_1 : MapIconValues.PLAYER_2;
    }
    TrimFirePathToEnvironment(origPath) {
        let updatedPath = [];
        updatedPath.push(origPath[0].clone());
        let foundTermination = false;
        for (let i = 0; i < origPath.length; ++i) {
            if (foundTermination)
                break;
            //logger.info(`*** Orig Path ${i} - (${origPath[i].x}, ${origPath[i].y})`);
            let coords = this.ClientPositionToMapCoordinates(origPath[i]);
            if (coords == null) {
                //We're shooting over the map, keep the position
                updatedPath.push(origPath[i].clone());
            }
            else {
                try {
                    //Check to see what is here
                    switch (this.mapMatrix[coords.x][coords.y]) {
                        case MapIconValues.EMPTY:
                            updatedPath.push(origPath[i].clone());
                            break;
                        case MapIconValues.GROUND:
                            foundTermination = true;
                            //Create a new position thats 0.5 up from the ground block
                            let pos = new three_1.Vector3(coords.x, coords.y, 0);
                            pos.x = origPath[i].x;
                            pos.y += 0.5;
                            updatedPath.push(pos);
                            break;
                    }
                }
                catch (err) {
                    logger.error(`Error getting map coordinate: ${coords.x}, ${coords.y} - Orig Path: ${origPath[i].x}, ${origPath[i].y}`);
                    throw `Error getting map coordinate: ${coords.x}, ${coords.y}`;
                }
            }
        }
        return updatedPath; // new CannonController.CannonFirePath(updatedPath.ToArray());
    }
    ClientPositionToMapCoordinates(clientPos) {
        let localPos = clientPos.clone();
        localPos.y = this.ClampNumber(localPos.y, 0.0, Number.MAX_VALUE);
        let coords = new three_1.Vector2(Math.round(localPos.x), Math.round(localPos.y));
        if (coords.y >= this.mapHeight) {
            return null; //We're shooting over the top
        }
        if (coords.x < 0 || coords.x >= this.mapWidth) {
            return null;
        }
        return coords;
    }
    ClampNumber(value, min, max) {
        if (value < min) {
            value = min;
        }
        else if (value > max) {
            value = max;
        }
        return value;
    }
    GetAvailableSpace(direction, startPosition) {
        let newX = startPosition.x;
        let newY = startPosition.y;
        newX += direction;
        if (newX < 0 || newX >= this.mapMatrix.length) {
            //Cant go off the right or left of the map, do nothing?
            return startPosition;
        }
        let mapValue = this.mapMatrix[newX][newY];
        if (mapValue == MapIconValues.EMPTY) {
            newY = this.FindNextSafeGridPos(newX, newY);
        }
        else if (mapValue == MapIconValues.GROUND) {
            //Don't pass the top of the map
            let yVal = Math.min(newY + 1, this.mapMatrix[0].length);
            let mapVal = this.mapMatrix[newX][yVal];
            if (mapVal == MapIconValues.EMPTY) {
                newY = yVal;
            }
            else {
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
        else {
            //Must be another player here, no movement!
            return startPosition;
        }
        return new three_1.Vector2(newX, newY);
    }
    //Get the next grid Y coordinate we can have a tank at
    FindNextSafeGridPos(startX, startY) {
        let groundFound = false;
        let yVal = Math.max(startY - 1, 0);
        while (!groundFound) {
            let mapVal = this.mapMatrix[startX][yVal];
            if (mapVal == MapIconValues.EMPTY) {
                //This space is empty, check lower
                startY = yVal;
                if (yVal == 0) {
                    //We've hit bedrock
                    groundFound = true;
                }
                else {
                    --yVal;
                }
            }
            else {
                groundFound = true;
            }
        }
        return startY;
    }
    dealDamage(localPosition, radius, damage) {
        let coords = this.localPositionToMapCoordinates(localPosition);
        if (coords == null) {
            logger.error("Explosion did not take place within the map matrix!");
            return null;
        }
        const updatedPlayersMap = new Map();
        let impactedCoordinates = this.getImpactedCoordinatesList(coords, radius);
        for (let i = 0; i < impactedCoordinates.length; ++i) {
            let damagedPlayers = this.dealDamageToCoordinates(impactedCoordinates[i]);
            if (damagedPlayers && damagedPlayers.length > 0) {
                for (let j = 0; j < damagedPlayers.length; j++) {
                    updatedPlayersMap.set(damagedPlayers[j], { playerId: damagedPlayers[j], damage });
                }
            }
        }
        // Get the player's with updated positions
        let updatedPlayersPositions = this.updateGrid();
        if (updatedPlayersPositions && updatedPlayersPositions.length > 0) {
            for (let i = 0; i < updatedPlayersPositions.length; i++) {
                this.SetPlayerPosition(updatedPlayersPositions[i].playerId, updatedPlayersPositions[i].playerPos);
                if (updatedPlayersMap.has(updatedPlayersPositions[i].playerId)) {
                    let playerObj = updatedPlayersMap.get(updatedPlayersPositions[i].playerId);
                    playerObj.playerPos = updatedPlayersPositions[i].playerPos;
                }
                else {
                    updatedPlayersMap.set(updatedPlayersPositions[i].playerId, { playerId: updatedPlayersPositions[i].playerId, playerPos: updatedPlayersPositions[i].playerPos });
                }
            }
        }
        const updatedPlayers = Array.from(updatedPlayersMap.values());
        return { impactedCoordinates, updatedPlayers };
    }
    dealDamageToCoordinates(coords) {
        let damagedPlayers;
        //Get what is at this position
        let mapItem = this.mapMatrix[coords.x][coords.y];
        switch (mapItem) {
            case MapIconValues.GROUND:
                {
                    //Ground goes boom?
                    this.mapMatrix[coords.x][coords.y] = MapIconValues.EMPTY;
                    break;
                }
            case MapIconValues.PLAYER_1:
            case MapIconValues.PLAYER_2:
                {
                    if (damagedPlayers == null) {
                        damagedPlayers = [];
                    }
                    if (mapItem == MapIconValues.PLAYER_1) {
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
    updateGrid() {
        let updatedPlayers = [];
        //Check player positions, if any are floating, they need to be moved down
        for (let x = 0; x < this.mapWidth; ++x) {
            for (let y = 0; y < this.mapHeight; ++y) {
                let mapValue = this.mapMatrix[x][y];
                switch (mapValue) {
                    case MapIconValues.PLAYER_1:
                    case MapIconValues.PLAYER_2:
                        {
                            let playerId = mapValue == MapIconValues.PLAYER_1 ? 0 : 1;
                            let newY = this.FindNextSafeGridPos(x, y);
                            if (newY != y) {
                                let playerPos = new three_1.Vector2(x, newY);
                                this.updatePlayerPos(mapValue == MapIconValues.PLAYER_1, new three_1.Vector2(x, y), playerPos);
                                updatedPlayers.push({ playerId, playerPos });
                            }
                            break;
                        }
                }
            }
        }
        return updatedPlayers;
    }
    getImpactedCoordinatesList(coords, radius) {
        let impactedPoints = [];
        let origX = coords.x;
        let origY = coords.y;
        let left = 0;
        let right = 0;
        let up = 0;
        let down = 0;
        //0 is the origin, start at 1 and go to radius
        for (let i = 1; i <= radius; ++i) {
            left = origX - i;
            right = origX + i;
            up = origY + i;
            down = origY - i;
            //Iterate from left to right
            for (let x = left; x <= right; ++x) {
                //Check if we're within the horizontal bounds of the map
                if (x >= 0 && x < this.mapWidth) {
                    //Iterate from bottom to top
                    for (let y = down; y <= up; ++y) {
                        //Check if we're within the vertical bounds of the map
                        if (y >= 0 && y < this.mapHeight) {
                            let pos = new three_1.Vector2(x, y);
                            if (!impactedPoints.includes(pos)) {
                                impactedPoints.push(pos);
                            }
                        }
                    }
                }
            }
        }
        return impactedPoints;
    }
    localPositionToMapCoordinates(localPos) {
        localPos.y = this.ClampNumber(localPos.y, 0.0, Number.MAX_VALUE);
        let coords = new three_1.Vector2(Math.round(localPos.x), Math.round(localPos.y));
        if (coords.y >= this.mapHeight) {
            return null; //We're shooting over the top
        }
        if (coords.x < 0 || coords.x >= this.mapWidth) {
            return null;
        }
        return coords;
    }
    updatePlayerPos(playerOne, previousCoordinates, newCoordinates) {
        // Set the player's old spot to empty
        this.mapMatrix[previousCoordinates.x][previousCoordinates.y] = MapIconValues.EMPTY;
        // Set the player's new spot to have the player
        this.mapMatrix[newCoordinates.x][newCoordinates.y] = playerOne ? MapIconValues.PLAYER_1 : MapIconValues.PLAYER_2;
    }
}
exports.EnvironmentBuilder = EnvironmentBuilder;
