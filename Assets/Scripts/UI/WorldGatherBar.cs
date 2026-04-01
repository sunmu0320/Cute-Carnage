using UnityEngine;
using UnityEngine.UI;

public class WorldGatherBar : MonoBehaviour
{
    [SerializeField] Image fillImage;

    void Awake()
    {
        HideInstant();
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void HideInstant()
    {
        SetProgress(0f);
        gameObject.SetActive(false);
    }

    public void SetProgress(float normalized)
    {
        if (fillImage == null)
            return;

        fillImage.fillAmount = Mathf.Clamp01(normalized);
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        transform.rotation = cam.transform.rotation;
    }
}
