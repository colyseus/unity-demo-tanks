using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CannonBase
{
    //public string name;

    //[Tooltip("The max we can charge up this cannon shot")]
    //public float maximumCharge = 1.0f;
    //[Tooltip("How long it takes to reach a full charge")]
    //public float chargeTime = 3.0f;
    //[Tooltip("How many grid places in each direction will be impacted")]
    //public int radius = 1;
    //[Tooltip("How much damage the projectile will deal")]
    //public float impactDamage = 1.0f;

    public GameObject projectilePrefab;
    public LineRenderer trajectoryLine;
    public ParticleSystem fireEffect;
}
