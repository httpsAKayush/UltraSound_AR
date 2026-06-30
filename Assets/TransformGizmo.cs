using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class TransformGizmo : MonoBehaviour
{
    [Header("Target")]
    public Transform bodyRoot;

    [Header("Gizmo Settings")]
    public float gizmoRadius = 0.3f;
    public float ringThickness = 0.01f;
    public float arrowLength = 0.4f;
    public float handleSize = 0.03f;

    private Material matX, matY, matZ, matW, matHighlight;

    private GameObject ringX, ringY, ringZ;
    private GameObject gizmoRotationRoot;

    private GameObject arrowX, arrowY, arrowZ, arrowUniform;
    private GameObject gizmoScaleRoot;

    private bool showRotation = false;
    private bool showScale = false;
    private GameObject activeHandle = null;
    private Vector3 lastControllerPos;
    private Vector3 lastControllerDir;
    private bool triggerWasPressed = false;

    private InputDevice leftDevice;
    private InputDevice rightDevice;
    private Transform rightControllerTransform;
    private LineRenderer rayLine;

    private bool aWasPressed = false;
    private bool xWasPressed = false;

    void Start()
    {
        CreateMaterials();
        CreateRotationGizmo();
        CreateScaleGizmo();

        rayLine = gameObject.AddComponent<LineRenderer>();
        rayLine.startWidth = 0.003f;
        rayLine.endWidth = 0.001f;
        rayLine.material = matW;
        rayLine.enabled = false;

        gizmoRotationRoot.SetActive(false);
        gizmoScaleRoot.SetActive(false);
    }

    void Update()
    {
        if (!leftDevice.isValid)
            leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!rightDevice.isValid)
            rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (rightControllerTransform == null)
        {
            var go = GameObject.Find("Right Controller");
            if (go != null) rightControllerTransform = go.transform;
        }

        HandleButtonToggles();
        UpdateGizmoPositions();
        HandleRayInteraction();

#if UNITY_EDITOR
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.digit1Key.wasPressedThisFrame) ToggleRotation();
            if (UnityEngine.InputSystem.Keyboard.current.digit2Key.wasPressedThisFrame) ToggleScale();
        }
#endif
    }

    void HandleButtonToggles()
    {
        bool aPressed = false;
        rightDevice.TryGetFeatureValue(CommonUsages.primaryButton, out aPressed);
        if (aPressed && !aWasPressed) ToggleRotation();
        aWasPressed = aPressed;

        bool xPressed = false;
        leftDevice.TryGetFeatureValue(CommonUsages.primaryButton, out xPressed);
        if (xPressed && !xWasPressed) ToggleScale();
        xWasPressed = xPressed;
    }

    void ToggleRotation()
    {
        showRotation = !showRotation;
        if (showRotation) showScale = false;
        gizmoRotationRoot.SetActive(showRotation);
        gizmoScaleRoot.SetActive(showScale);
        activeHandle = null;
    }

    void ToggleScale()
    {
        showScale = !showScale;
        if (showScale) showRotation = false;
        gizmoScaleRoot.SetActive(showScale);
        gizmoRotationRoot.SetActive(showRotation);
        activeHandle = null;
    }

    void UpdateGizmoPositions()
    {
        if (bodyRoot == null) return;
        gizmoRotationRoot.transform.position = bodyRoot.position;
        gizmoRotationRoot.transform.rotation = Quaternion.identity;
        gizmoScaleRoot.transform.position = bodyRoot.position;
        gizmoScaleRoot.transform.rotation = Quaternion.identity;
    }

    void HandleRayInteraction()
    {
        if (!showRotation && !showScale)
        {
            rayLine.enabled = false;
            return;
        }

        if (rightControllerTransform == null) return;

        Vector3 rayOrigin = rightControllerTransform.position;
        Vector3 rayDir = rightControllerTransform.forward;

        rayLine.enabled = true;
        rayLine.SetPosition(0, rayOrigin);
        rayLine.SetPosition(1, rayOrigin + rayDir * 1.5f);

        bool triggerPressed = false;
        rightDevice.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed);

        if (triggerPressed && !triggerWasPressed)
        {
            activeHandle = GetRayHitHandle(rayOrigin, rayDir);
            if (activeHandle != null)
            {
                lastControllerPos = rightControllerTransform.position;
                lastControllerDir = rayDir;
                HighlightHandle(activeHandle);
            }
        }

        if (triggerPressed && activeHandle != null)
        {
            Vector3 controllerDelta = rightControllerTransform.position - lastControllerPos;
            ApplyTransform(activeHandle, controllerDelta);
            lastControllerPos = rightControllerTransform.position;
        }

        if (!triggerPressed && triggerWasPressed)
        {
            if (activeHandle != null) ResetHandleColor(activeHandle);
            activeHandle = null;
        }

        triggerWasPressed = triggerPressed;
    }

    GameObject GetRayHitHandle(Vector3 origin, Vector3 dir)
    {
        Ray ray = new Ray(origin, dir);
        float closest = float.MaxValue;
        GameObject hit = null;

        List<GameObject> handles = showRotation
            ? new List<GameObject> { ringX, ringY, ringZ }
            : new List<GameObject> { arrowX, arrowY, arrowZ, arrowUniform };

        foreach (var handle in handles)
        {
            if (handle == null) continue;
            Collider[] cols = handle.GetComponentsInChildren<Collider>();
            foreach (var col in cols)
            {
                RaycastHit info;
                if (col.Raycast(ray, out info, 2f))
                {
                    if (info.distance < closest)
                    {
                        closest = info.distance;
                        hit = handle;
                    }
                }
            }
        }
        return hit;
    }

    void ApplyTransform(GameObject handle, Vector3 delta)
    {
        if (showRotation)
        {
            float speed = 200f;
            if (handle == ringX)
                bodyRoot.Rotate(Vector3.right, -delta.y * speed, Space.World);
            else if (handle == ringY)
                bodyRoot.Rotate(Vector3.up, delta.x * speed, Space.World);
            else if (handle == ringZ)
                bodyRoot.Rotate(Vector3.forward, -delta.x * speed, Space.World);
        }
        else if (showScale)
        {
            float speed = 2f;

            if (handle == arrowUniform)
            {
                bodyRoot.localScale += Vector3.one * delta.y * speed;
                bodyRoot.localScale = Vector3.Max(bodyRoot.localScale, Vector3.one * 0.05f);
            }
            else if (handle == arrowX)
            {
                Vector3 s = bodyRoot.localScale;
                s.x += delta.x * speed;
                s.x = Mathf.Max(s.x, 0.05f);
                bodyRoot.localScale = s;
            }
            else if (handle == arrowY)
            {
                Vector3 s = bodyRoot.localScale;
                s.y += delta.y * speed;
                s.y = Mathf.Max(s.y, 0.05f);
                bodyRoot.localScale = s;
            }
            else if (handle == arrowZ)
            {
                Vector3 s = bodyRoot.localScale;
                s.z += delta.z * speed;
                s.z = Mathf.Max(s.z, 0.05f);
                bodyRoot.localScale = s;
            }
        }
    }

    void HighlightHandle(GameObject handle)
    {
        foreach (var mr in handle.GetComponentsInChildren<MeshRenderer>())
            mr.material = matHighlight;
    }

    void ResetHandleColor(GameObject handle)
    {
        Material mat = null;
        if (handle == ringX || handle == arrowX) mat = matX;
        else if (handle == ringY || handle == arrowY) mat = matY;
        else if (handle == ringZ || handle == arrowZ) mat = matZ;
        else mat = matW;

        foreach (var mr in handle.GetComponentsInChildren<MeshRenderer>())
            mr.material = mat;
    }

    void CreateMaterials()
    {
        matX = CreateUnlitMaterial(new Color(1f, 0.2f, 0.2f));
        matY = CreateUnlitMaterial(new Color(0.2f, 1f, 0.2f));
        matZ = CreateUnlitMaterial(new Color(0.2f, 0.4f, 1f));
        matW = CreateUnlitMaterial(new Color(1f, 1f, 1f, 0.8f));
        matHighlight = CreateUnlitMaterial(new Color(1f, 0.9f, 0f));
    }

    Material CreateUnlitMaterial(Color color)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.color = color;
        return m;
    }

    void CreateRotationGizmo()
    {
        gizmoRotationRoot = new GameObject("GizmoRotation");

        ringX = CreateRing("RingX", matX);
        ringX.transform.SetParent(gizmoRotationRoot.transform);
        ringX.transform.localRotation = Quaternion.Euler(0, 90, 0);

        ringY = CreateRing("RingY", matY);
        ringY.transform.SetParent(gizmoRotationRoot.transform);
        ringY.transform.localRotation = Quaternion.Euler(90, 0, 0);

        ringZ = CreateRing("RingZ", matZ);
        ringZ.transform.SetParent(gizmoRotationRoot.transform);
        ringZ.transform.localRotation = Quaternion.Euler(0, 0, 0);
    }

    GameObject CreateRing(string name, Material mat)
    {
        GameObject root = new GameObject(name);
        int segments = 16;

        GameObject meshObj = new GameObject(name + "_mesh");
        meshObj.transform.SetParent(root.transform);
        meshObj.transform.localPosition = Vector3.zero;
        meshObj.transform.localRotation = Quaternion.identity;
        MeshFilter mf = meshObj.AddComponent<MeshFilter>();
        MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mf.mesh = BuildRingMesh(gizmoRadius, ringThickness, 48, 8);

        root.AddComponent<MeshFilter>().mesh = BuildRingMesh(gizmoRadius, ringThickness, 48, 8);
        MeshRenderer rootMr = root.AddComponent<MeshRenderer>();
        rootMr.material = mat;
        rootMr.enabled = false;

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * gizmoRadius, Mathf.Sin(angle) * gizmoRadius, 0);

            GameObject colObj = new GameObject("col_" + i);
            colObj.transform.SetParent(root.transform);
            colObj.transform.localPosition = pos;
            SphereCollider sc = colObj.AddComponent<SphereCollider>();
            sc.radius = ringThickness * 4f;
        }

        return root;
    }

    Mesh BuildRingMesh(float radius, float tube, int ringSegs, int tubeSegs)
    {
        Mesh mesh = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        for (int i = 0; i <= ringSegs; i++)
        {
            float u = (float)i / ringSegs * Mathf.PI * 2f;
            Vector3 center = new Vector3(Mathf.Cos(u) * radius, Mathf.Sin(u) * radius, 0);
            Vector3 outward = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0);

            for (int j = 0; j <= tubeSegs; j++)
            {
                float v = (float)j / tubeSegs * Mathf.PI * 2f;
                Vector3 vert = center + (outward * Mathf.Cos(v) + Vector3.forward * Mathf.Sin(v)) * tube;
                verts.Add(vert);
            }
        }

        for (int i = 0; i < ringSegs; i++)
        {
            for (int j = 0; j < tubeSegs; j++)
            {
                int a = i * (tubeSegs + 1) + j;
                int b = a + tubeSegs + 1;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(b); tris.Add(b + 1); tris.Add(a + 1);
            }
        }

        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }

    void CreateScaleGizmo()
    {
        gizmoScaleRoot = new GameObject("GizmoScale");

        arrowX = CreateArrow("ArrowX", matX);
        arrowY = CreateArrow("ArrowY", matY);
        arrowZ = CreateArrow("ArrowZ", matZ);
        arrowUniform = CreateCube("ArrowUniform", matW, handleSize * 1.5f);

        arrowX.transform.SetParent(gizmoScaleRoot.transform);
        arrowY.transform.SetParent(gizmoScaleRoot.transform);
        arrowZ.transform.SetParent(gizmoScaleRoot.transform);
        arrowUniform.transform.SetParent(gizmoScaleRoot.transform);

        arrowX.transform.localRotation = Quaternion.Euler(0, 0, -90);
        arrowX.transform.localPosition = new Vector3(arrowLength * 0.5f, 0, 0);

        arrowY.transform.localRotation = Quaternion.identity;
        arrowY.transform.localPosition = new Vector3(0, arrowLength * 0.5f, 0);

        arrowZ.transform.localRotation = Quaternion.Euler(90, 0, 0);
        arrowZ.transform.localPosition = new Vector3(0, 0, arrowLength * 0.5f);

        arrowUniform.transform.localPosition = Vector3.zero;
    }

    GameObject CreateArrow(string name, Material mat)
    {
        GameObject go = new GameObject(name);

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.transform.SetParent(go.transform);
        shaft.transform.localScale = new Vector3(ringThickness * 2f, arrowLength * 0.5f, ringThickness * 2f);
        shaft.transform.localPosition = Vector3.zero;
        shaft.GetComponent<MeshRenderer>().material = mat;
        Destroy(shaft.GetComponent<CapsuleCollider>());

        GameObject head = CreateCube(name + "_head", mat, handleSize);
        head.transform.SetParent(go.transform);
        head.transform.localPosition = new Vector3(0, arrowLength * 0.5f, 0);

        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.size = new Vector3(handleSize, arrowLength, handleSize);
        bc.center = Vector3.zero;

        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>().material = mat;

        return go;
    }

    GameObject CreateCube(string name, Material mat, float size)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.localScale = Vector3.one * size;
        go.GetComponent<MeshRenderer>().material = mat;
        return go;
    }
}