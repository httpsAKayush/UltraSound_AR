using UnityEngine;

public class UltrasoundMonitor : MonoBehaviour
{
    Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();

        if (feeee.Instance != null)
        {
            rend.material.mainTexture =
                feeee.Instance.FeedTexture;
        }
    }

    void Update()
    {
        if (feeee.Instance != null)
        {
            rend.material.mainTexture =
                feeee.Instance.FeedTexture;
        }
    }
}