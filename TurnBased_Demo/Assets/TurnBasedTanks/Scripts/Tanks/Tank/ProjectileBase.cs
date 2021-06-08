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

    private Vector2? _targetPosition;

    public void UpdateTargetPosition(Tanks.Vector2 position)
    {
        _targetPosition = new Vector2(position.x, position.y);
    }

    void Update()
    {
        if (_targetPosition == null)
        {
            return;
        }

        transform.localPosition = Vector3.Slerp(transform.localPosition, (Vector2)_targetPosition, moveSpeed * Time.deltaTime);
    }

    public void Explode()
    {
        gameObject.SetActive(false);

        Instantiate(explosionPrefab, transform.position, Quaternion.identity);
    }
}
