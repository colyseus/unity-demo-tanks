using System;
using System.Collections;
using System.Collections.Generic;
using LucidSightTools;
using Tanks;
using UnityEngine;

public class TankController : MonoBehaviour
{

    [SerializeField]
    private CannonController cannon;

    public int CurrentAP { get; set; }

    public bool ignoreAPReset;

    public int currentMovement { get; private set; }

    private bool canAct = false;

    private const float moveDuration = 2f;
    private TankGameManager.eTurn tanksTurn;

    public EnvironmentBuilder.MapCoordinates mapCoords { get; private set; }

    public Vector3 BarrelForward
    {
        get
        {
            return cannon.BarrelForward;
        }
    }

    public Vector3 BarrelPosition
    {
        get
        {
            return cannon.BarrelPosition;
        }
    }

    public float CannonPower
    {
        get
        {
            return cannon.CannonPower;
        }
    }

    public CannonBase ActiveWeapon
    {
        get
        {
            return cannon.ActiveWeapon;
        }
    }

    public Weapon ActiveWeaponData
    {
        get
        {
            return cannon.ActiveWeaponData;
        }
    }

    private readonly KeyCode[] weaponHotKeys = new KeyCode[] {KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3};

    public void Initialize(TankGameManager.eTurn turn)
    {
        ResetActions();
        tanksTurn = turn;
        cannon.SetAngle(-45.0f);
    }

    public void SetCoordinates(int x, int y)
    {
        mapCoords = new EnvironmentBuilder.MapCoordinates(x, y);
    }

    public void AllowAction(bool allowed)
    {
        canAct = allowed;
    }

    public bool CanAct()
    {
        return canAct && CurrentAP > 0;
    }

    public void Move(EnvironmentBuilder.MapCoordinates newCoords, bool playerOne)
    {
        MoveToNewCoordinates(newCoords, playerOne);
    }

    public void MoveToNewCoordinates(EnvironmentBuilder.MapCoordinates newCoords, bool playerOne)
    {
        if (gameObject.activeInHierarchy == false)
        {
            return;
        }

        canAct = false;

        Vector3 newPosition = TankGameManager.Instance.Builder.CoordinateToWorldPosition(newCoords);
        TankGameManager.Instance.Builder.MovePlayer(playerOne, mapCoords, newCoords);
        SetCoordinates(newCoords.x, newCoords.y);
        StartCoroutine(MoveTank(newPosition));
    }

    public void BeginTurn()
    {
        ResetActions();
    }

    public void Destroyed()
    {
        gameObject.SetActive(false);
        canAct = false;
    }

    public void StartChargeCannon()
    {
        if(!canAct || CurrentAP < GameRules.FiringAPCost)
            return;

        cannon.StartCharging();
    }

    public bool CheckHotkeys()
    {
        for (int i = 0; i < weaponHotKeys.Length; ++i)
        {
            if (Input.GetKeyDown(weaponHotKeys[i]))
            {
                ChangeWeapon(i);
                return true;
            }
        }

        return false;
    }

    private void ChangeWeapon(int desIndex)
    {
        if (!canAct)
            return;
        TanksColyseusManager.NetSend("changeWeapon", desIndex);
    }

    public void UpdateSelectedWeapon(Weapon weapon)
    {
        cannon.ChangeWeapon(weapon);
    }

    public void AbortCharge()
    {
        if (!canAct)
            return;

        cannon.EndCharging();
    }

    public float? AdjustAim(float delta)
    {
        if (!canAct)
            return null;

        return cannon.AdjustAim(delta);
    }

    public void SetAim(float aimAngle)
    {
        cannon.SetAngle(aimAngle);
    }

    private void ResetActions()
    {

        currentMovement = 0;

        if (!ignoreAPReset)
        {
            CurrentAP = GameRules.MaxAP;
        }
    }

    private IEnumerator MoveTank(Vector3 newPos)
    {
        transform.position = newPos;
        yield return new WaitForSeconds(moveDuration);

        canAct = true;

    }
}
