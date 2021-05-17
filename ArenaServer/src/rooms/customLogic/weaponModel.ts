
export class Weapon {
    index: number;
    name: string;
    maxCharge: number;
    chargeTime: number;
    radius: number;
    impactDamage: number;

    constructor(index: number, name: string, maxCharge: number, chargeTime: number, radius: number, impactDamage: number) {
        this.index = index;
        this.name = name;
        this.maxCharge = maxCharge;
        this.chargeTime = chargeTime;
        this.radius = radius;
        this.impactDamage = impactDamage;
    }
}