export const GameRules = {
    MaxActionPoints: 3,
    MovementActionPointCost: 1,
    FiringActionPointCost: 2,
    ProjectileSpeed: 30,
    MaxMovement: 3,
    MaxHitPoints: 1,
    MovementTime: 2,
}

export const weaponList = [
    {
        name: "Short Range",
        maxCharge: 5,
        chargeTime: 1,
        radius: 1,
        impactDamage: 1,
        index: 0
    },
    {
        name: "Mid Range",
        maxCharge: 8,
        chargeTime: 2,
        radius: 1,
        impactDamage: 1,
        index: 1
    },
    {
        name: "Long Range",
        maxCharge: 10,
        chargeTime: 5,
        radius: 1,
        impactDamage: 1,
        index: 2
    }
]
