"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.Weapon = void 0;
class Weapon {
    constructor(index, name, maxCharge, chargeTime, radius, impactDamage) {
        this.index = index;
        this.name = name;
        this.maxCharge = maxCharge;
        this.chargeTime = chargeTime;
        this.radius = radius;
        this.impactDamage = impactDamage;
    }
}
exports.Weapon = Weapon;
