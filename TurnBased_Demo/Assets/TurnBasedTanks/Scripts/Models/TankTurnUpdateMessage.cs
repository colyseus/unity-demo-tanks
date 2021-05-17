using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TankTurnUpdateMessage
{
    public List<List<int>> worldMap;
    public int playerTurn;
    public int playerTurnId;
    public WeaponModel currentWeapon;
    public int currentPlayerAP;
    public string turnData;
    public bool wasSkip;
    public string[] playerNames;
    public int[] playerHP;
    public bool challengerOnline;
}

[Serializable]
public class TankMoveMessage
{
    public int playerNumber;
    public int remainingAP;
    public Vector2 newCoords;
}

[Serializable]
public class FirePathMessage
{
    public int playerNumber;
    public int remainingAP;
    public List<Vector3> firePath;
    public DamageData damageData;
}

[Serializable]
public class SelectedWeaponUpdatedMessage
{
    public WeaponModel weapon;
}