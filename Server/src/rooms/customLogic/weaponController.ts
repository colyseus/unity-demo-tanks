const logger = require("../../helpers/logger");

import { Weapon } from "./weaponModel";
import { WeaponData } from "./gameRules"

export class WeaponController {
    
    weapons: Weapon[];
    playerWeapons: Map<number, Weapon> // <Player turnId, playerNum>

    constructor() {

        this.weapons = [];
        this.playerWeapons = new Map<number, Weapon>();

        for(let i = 0; i < WeaponData.length; i++){
            this.weapons.push(new Weapon(i, WeaponData[i].name, WeaponData[i].maxCharge, WeaponData[i].chargeTime, WeaponData[i].radius, WeaponData[i].impactDamage));
        }
    }

    public addPlayer(playerId: number) {

        if(this.playerWeapons.has(playerId) == false) {
            // Set the player's active weapon to be the first one
            this.playerWeapons.set(playerId, this.weapons[0]);
        }
        else {
            logger.silly(`*** Weapon Controller - Player ${playerId} is already tracked ***`);
        }
    }

    public removePlayer(playerId: number) {
        if(this.playerWeapons.has(playerId)) {
            this.playerWeapons.delete(playerId);
        }
        else {
            logger.error(`*** Weapon Controller - Remove Player - No player with that turn Id - ${playerId} ***`);
        }
    }

    public getPlayerActiveWeapon(playerId: number): Weapon {
        
        if(this.playerWeapons.has(playerId)) {
            return this.playerWeapons.get(playerId);
        }

        return null;
    }

    public switchPlayerWeapon(playerId: number, desiredWeapon: number): Weapon {
        
        if(this.playerWeapons.has(playerId) == false) {

            logger.error(`*** Weapon Controller - No player for Id ${playerId}`);
            return null;
        }

        if(desiredWeapon > this.weapons.length - 1) {
            
            logger.error(`*** Desired weapon is invalid ***`);
            return null;
        }

        let weapon: Weapon = this.weapons[desiredWeapon];

        this.playerWeapons.set(playerId, weapon);

        return weapon;
    }

    public resetWeapons() {
        this.playerWeapons.forEach((weapon, player) => {
            weapon = this.weapons[0];
        });
    }
}