using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileBase : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed = 1.0f;

    [SerializeField]
    private float explodeDelay = 0.5f;

    [SerializeField]
    private GameObject explosionPrefab;

    private int currentPathIndex = 0;

    private CannonController.CannonFirePath currentTravelPath;
    private Action onImpact;

    private bool followProjectile = false;

    private CannonBase currentWeapon;
    private DamageData _damageData;

    public void HandPath(CannonController.CannonFirePath path, Action onComplete, CannonBase weapon, DamageData damageData)
    {
        currentTravelPath = path;
        currentPathIndex = 0;
        onImpact = onComplete;
        _damageData = damageData;

        //Maybe a setting or parameter
        followProjectile = true;

        currentWeapon = weapon;
    }

    void Update()
    {
        if (currentTravelPath == null)
            return;

        transform.localPosition/*position*/ = Vector3.MoveTowards(transform.localPosition/*position*/, currentTravelPath.path[currentPathIndex], moveSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.localPosition, currentTravelPath.path[currentPathIndex]) < 0.05f)
        {
            ++currentPathIndex;
            if (currentPathIndex >= currentTravelPath.path.Length)
            {
                //Boom
                currentTravelPath = null;
                Invoke("Explode", explodeDelay);
            }
        }

        if (followProjectile)
        {
            TankGameManager.Instance.FocusOnPosition(transform.position, false, null);
        }
    }

    void Explode()
    {
        Destroy(gameObject);
        GameObject effect = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        onImpact?.Invoke();

        TankGameManager.Instance.RegisterExplosion(_damageData);
    }
}
