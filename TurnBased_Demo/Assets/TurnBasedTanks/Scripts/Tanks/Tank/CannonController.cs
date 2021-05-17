using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CannonController : MonoBehaviour
{
    [Serializable]
    public class CannonFirePath
    {
        public Vector3[] path;

        public CannonFirePath(Vector3[] _path)
        {
            path = _path;
        }
    }

    public float CannonPower
    {
        get
        {
            return velocityCharge;
        }
    }

    public Vector3 BarrelForward
    {
        get
        {
            return barrelTip.forward;
        }
    }

    public Vector3 BarrelPosition
    {
        get
        {
            return barrelTip.position;
        }
    }

    [SerializeField]
    Transform cannonPivot;
    
    [SerializeField]
    private float rotationMultiplier = 5.0f;

    [SerializeField]
    private float cannonRotateSpeed = 5.0f;

    private float currentAimAngle = 0.0f;
    private float velocityCharge = 0.0f;

    private float FiringVelocity
    {
        get
        {
            return velocityCharge;
        }
    }

    private Coroutine chargingRoutine;

    [SerializeField]
    private Transform barrelTip;

    [SerializeField]
    private CannonBase[] weapons;

    public CannonBase ActiveWeapon
    {
        get
        {
            return weapons[currentWeapon];
        }
    }

    public WeaponModel ActiveWeaponData { get; private set; }

    private int currentWeapon = 0;

    private LineRenderer chargingLine { get { return ActiveWeapon.trajectoryLine; } }
    private ParticleSystem cannonFireEffect { get { return ActiveWeapon.fireEffect; } }
    public float MaxVelocity { get { return ActiveWeaponData.maxCharge; } }
    public float ChargePingTime { get { return ActiveWeaponData.chargeTime; } }
    public GameObject ProjectilePrefab {get { return ActiveWeapon.projectilePrefab; } }

    private CannonFirePath currentFirePath;
    public float FireDelay
    {
        get
        {
            return 1.0f;    //Wont need this, will attach to cannon fire
        }
    }

    public void SetAngle(float angle)
    {
        currentAimAngle = angle;
        AimCannonToAngle();
    }

    public float AdjustAim(float delta)
    {
        currentAimAngle += delta * rotationMultiplier;
        AimCannonToAngle();

        return currentAimAngle;
    }

    public void ChangeWeapon(WeaponModel weapon)
    {
        ActiveWeapon.trajectoryLine.positionCount = 0;

        ActiveWeaponData = weapon;
        currentWeapon = Mathf.Clamp(weapon.index, 0, weapons.Length - 1);
    }

    public void StartCharging()
    {
        chargingRoutine = StartCoroutine(ChargingRoutine());
    }

    public void EndCharging()
    {
        if(chargingRoutine != null)
        {
            StopCoroutine(chargingRoutine);
            chargingRoutine = null;
            velocityCharge = 0;
            SetTrajectoryLine();
        }
    }

    public void FireCannon(CannonFirePath firePath, DamageData damageData, Action onComplete = null)
    {
        cannonFireEffect.Play();
        currentFirePath = firePath;
        FireProjectile(currentFirePath, damageData, onComplete);
    }

    public void FireProjectile(CannonFirePath path, DamageData damageData, Action onComplete)
    {
        GameObject projectile = Instantiate(ProjectilePrefab);
        projectile.transform.SetParent(TankGameManager.Instance.Builder.groundPieceRoot);
        projectile.transform.localPosition = path.path[0];
        projectile.GetComponent<ProjectileBase>().HandPath(path, onComplete, ActiveWeapon, damageData);
    }

    private void AimCannonToAngle()
    {
        currentAimAngle = Mathf.Clamp(currentAimAngle, -90.0f, 90.0f);
        cannonPivot.localRotation = Quaternion.Euler(0, 0, currentAimAngle);
    }

    private void SetTrajectoryLine()
    {
        if (velocityCharge == 0.0f)
        {
            chargingLine.positionCount = 1;
            chargingLine.SetPosition(0, barrelTip.position);
        }
        else
        {
            currentFirePath = TankGameManager.Instance.EstimateFirePath(FiringVelocity, barrelTip);
            chargingLine.positionCount = currentFirePath.path.Length;
            chargingLine.SetPositions(currentFirePath.path);
        }
    }

    IEnumerator ChargingRoutine()
    {
        velocityCharge = 0.0f;
        float t = 0.0f;
        bool increasing = true;
        while (true)
        {
            velocityCharge = Mathf.Lerp(increasing ? 0.0f : MaxVelocity, increasing ? MaxVelocity : 0.0f, t / ChargePingTime);
            SetTrajectoryLine();
            yield return new WaitForEndOfFrame();
            t += Time.deltaTime;
            if (t >= ChargePingTime)
            {
                t = 0.0f;
                increasing = !increasing;
            }
        }
    }

    //private Vector3 endPos;
    void OnDrawGizmos()
    {
        if (currentFirePath != null)
        {
            for (int i = 0; i < currentFirePath.path.Length; ++i)
            {
                Gizmos.DrawSphere(TankGameManager.Instance.Builder.groundPieceRoot.position + currentFirePath.path[i], 0.25f);
            }
        }
    }
}
