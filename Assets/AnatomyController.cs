using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class AnatomyController : MonoBehaviour
{
    [Header("Calibration")]
    public Transform bodyRoot;
    public Transform leftController;
    public Transform rightController;

    [Header("Proximity Detection")]
    public float proximityRadius = 0.05f;

    [Header("Organ Materials")]
    public Material defaultMaterial;
    public Material highlightMaterial;

    private Dictionary<string, MeshRenderer> organRenderers = new();
    private string currentOrgan = "";
    private bool calibrating = false;
    private Vector3 calibrationStartPos;
    private Vector3 bodyStartPos;

    private InputDevice leftDevice;
    private InputDevice rightDevice;

    void Start()
    {
        foreach (Transform child in bodyRoot.GetComponentsInChildren<Transform>())
        {
            MeshRenderer mr = child.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                organRenderers[child.name] = mr;
                if (defaultMaterial != null)
                    mr.material = defaultMaterial;
            }
        }
        Debug.Log($"AnatomyController: registered {organRenderers.Count} organ meshes");
    }

    void Update()
    {
        if (!leftDevice.isValid)
            leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!rightDevice.isValid)
            rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        HandleCalibration();
        HandleProximity();
    }

    void HandleCalibration()
    {
        bool leftGrip = false;
        leftDevice.TryGetFeatureValue(CommonUsages.gripButton, out leftGrip);

        bool rightGrip = false;
        rightDevice.TryGetFeatureValue(CommonUsages.gripButton, out rightGrip);

        Vector2 rightStick = Vector2.zero;
        rightDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightStick);

        if (leftGrip)
        {
            if (!calibrating)
            {
                calibrating = true;
                calibrationStartPos = leftController.position;
                bodyStartPos = bodyRoot.position;
            }
            Vector3 delta = leftController.position - calibrationStartPos;
            bodyRoot.position = bodyStartPos + delta;
        }
        else
        {
            calibrating = false;
        }

        if (Mathf.Abs(rightStick.y) > 0.1f)
        {
            float scaleDelta = rightStick.y * Time.deltaTime * 0.5f;
            bodyRoot.localScale += Vector3.one * scaleDelta;
            bodyRoot.localScale = Vector3.Max(bodyRoot.localScale, Vector3.one * 0.1f);
        }

        if (rightGrip && Mathf.Abs(rightStick.x) > 0.1f)
        {
            bodyRoot.Rotate(0, rightStick.x * Time.deltaTime * 90f, 0, Space.World);
        }
    }

    void HandleProximity()
    {
        Vector3 toolPos = rightController.position;
        //Vector3 toolPos = Camera.main.transform.position + Camera.main.transform.forward * 0.5f;
        string closestOrgan = "";
        float closestDist = proximityRadius;

        foreach (var kvp in organRenderers)
        {
            if (kvp.Value == null) continue;
            Bounds bounds = kvp.Value.bounds;
            float distToBounds = Vector3.Distance(toolPos, bounds.ClosestPoint(toolPos));

            if (distToBounds < closestDist)
            {
                closestDist = distToBounds;
                closestOrgan = kvp.Key;
            }
        }

        if (closestOrgan != currentOrgan)
        {
            if (currentOrgan != "" && organRenderers.ContainsKey(currentOrgan))
                organRenderers[currentOrgan].material = defaultMaterial;

            currentOrgan = closestOrgan;

            if (currentOrgan != "")
            {
                if (highlightMaterial != null)
                    organRenderers[currentOrgan].material = highlightMaterial;

                Debug.Log($"Tool near: {currentOrgan}");
            }
        }
    }
}