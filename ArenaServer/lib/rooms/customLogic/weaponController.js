"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.WeaponController = void 0;
const logger = require("../../helpers/logger");
const weaponModel_1 = require("./weaponModel");
const gameRules_1 = require("./gameRules");
class WeaponController {
    constructor() {
        this.weapons = [];
        this.playerWeapons = new Map();
        for (let i = 0; i < gameRules_1.WeaponData.length; i++) {
            this.weapons.push(new weaponModel_1.Weapon(i, gameRules_1.WeaponData[i].name, gameRules_1.WeaponData[i].maxCharge, gameRules_1.WeaponData[i].chargeTime, gameRules_1.WeaponData[i].radius, gameRules_1.WeaponData[i].impactDamage));
        }
    }
    addPlayer(playerId) {
        if (this.playerWeapons.has(playerId) == false) {
            // Set the player's active weapon to be the first one
            this.playerWeapons.set(playerId, this.weapons[0]);
        }
        else {
            logger.silly(`*** Weapon Controller - Player ${playerId} is already tracked ***`);
        }
    }
    removePlayer(playerId) {
        if (this.playerWeapons.has(playerId)) {
            this.playerWeapons.delete(playerId);
        }
        else {
            logger.error(`*** Weapon Controller - Remove Player - No player with that turn Id - ${playerId} ***`);
        }
    }
    getPlayerActiveWeapon(playerId) {
        if (this.playerWeapons.has(playerId)) {
            return this.playerWeapons.get(playerId);
        }
        return null;
    }
    switchPlayerWeapon(playerId, desiredWeapon) {
        if (this.playerWeapons.has(playerId) == false) {
            logger.error(`*** Weapon Controller - No player for Id ${playerId}`);
            return null;
        }
        if (desiredWeapon > this.weapons.length - 1) {
            logger.error(`*** Desired weapon is invalid ***`);
            return null;
        }
        let weapon = this.weapons[desiredWeapon];
        this.playerWeapons.set(playerId, weapon);
        return weapon;
    }
    resetWeapons() {
        this.playerWeapons.forEach((weapon, player) => {
            weapon = this.weapons[0];
        });
    }
}
exports.WeaponController = WeaponController;
