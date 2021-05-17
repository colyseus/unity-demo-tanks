using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Wrapper class representation of a single turn from a player
[Serializable]
public class TurnActionData
{
    public enum eActionType
    {
        MOVEMENT,
        FIRE,
        SKIP
    }

    public eActionType actionType;
    public int[,] mapDataBeforeAction;
    public int[,] mapDataAfterAction;
    public int apCost;  //Probably don't need?

    //Variables for cannon fire
    public Vector3 barrelForward;
    public Vector3 barrelPosition;
    public float cannonPower;
}

[Serializable]
public class TurnContainer
{
    public int turnNumber;
    public TurnActionData[] turnData;

    public TurnActionData LastAction
    {
        get
        {
            return turnData[turnData.Length - 1];
        }
    }

    public TurnActionData FirstAction
    {
        get
        {
            return turnData[0];
        }
    }

    public int NumberActions
    {
        get
        {
            return turnData.Length;
        }
    }
}

