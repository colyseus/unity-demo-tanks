using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public Vector3 maxDistance; //Y and Z values
    public Vector3 minDistance;
    public float transitionSpeed = 1.0f;

    public float zoomSpeed = 1.0f;
    private float currZoom = 1.0f;

    private Vector3 desiredPosition;
    private Action arrivalCallback;

    private bool isTracking = false;

    void Awake()
    {
        desiredPosition = transform.position;
    }

    public void FocusOnPosition(Vector3 worldPos, bool overrideZoom = false, Action onArrival = null)
    {
        //Update where we zoom in to
        minDistance.x = worldPos.x;
        desiredPosition = worldPos;
        if (overrideZoom)
        {
            //Then zoom all the way in
            currZoom = 1.0f;
        }

        arrivalCallback = onArrival;
        UpdateZoomPosition();
    }

    void FixedUpdate()
    {
        HandleZoom();

        if (!isTracking)
            return;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * transitionSpeed);
        if (Vector3.Distance(transform.position, desiredPosition) <= 0.1f)
        {
            isTracking = false;
            arrivalCallback?.Invoke();
            arrivalCallback = null;
        }
    }

    private void HandleZoom()
    {
        if (Input.GetAxis("Mouse ScrollWheel") > 0f) // forward
        {
            currZoom += zoomSpeed * Time.deltaTime;
            currZoom = Mathf.Clamp01(currZoom);
            UpdateZoomPosition();
        }
        else if (Input.GetAxis("Mouse ScrollWheel") < 0f ) // backwards
        {
            currZoom -= zoomSpeed * Time.deltaTime;
            currZoom = Mathf.Clamp01(currZoom);
            UpdateZoomPosition();
        }
    }

    private void UpdateZoomPosition()
    {
        isTracking = true;
        desiredPosition = Vector3.Lerp(maxDistance, minDistance, currZoom);
    }
}
