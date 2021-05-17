const GameRules = {
    MovementAPCost : 1,
    FiringAPCost :  2,
    MaxAP : 3,
    MaxMovement : 3,
    MaxHitPoints: 3,
    MovementTime: 2
}

const WeaponData = [
    {
        name: "Short Range",
        maxCharge: 5,
        chargeTime: 1,
        radius: 1,
        impactDamage: 1
    },
    {
        name: "Mid Range",
        maxCharge: 8,
        chargeTime: 2,
        radius: 1,
        impactDamage: 1
    },
    {
        name: "Long Range",
        maxCharge: 10,
        chargeTime: 5,
        radius: 1,
        impactDamage: 1
    }
]

export { GameRules, WeaponData }