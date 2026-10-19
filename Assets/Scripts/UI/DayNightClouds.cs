using UnityEngine;

/// <summary>Decorative cloud drift around the dial center (right -> top -> left), independent of game time
/// and kill progress. Deliberate per-frame exception to the no-Update UI rule: it is continuous animation,
/// not state polling. Uses scaled deltaTime, so the pause menu (timeScale 0) freezes it.</summary>
public class DayNightClouds : MonoBehaviour
{
    [SerializeField] private RectTransform[] clouds;
    [Tooltip("Start angle per cloud in degrees (0 = right, 90 = top).")]
    [SerializeField] private float[] startAngles = { 30f, 210f };
    [SerializeField] private Vector2 orbitCenter = Vector2.zero;
    [SerializeField] private Vector2 orbitRadius = new Vector2(38f, 30f);
    [Tooltip("Seconds per full turn.")]
    [SerializeField] private float period = 150f;

    private float angle;

    private void Update()
    {
        if (period <= 0f)
            return;

        angle += Time.deltaTime * (Mathf.PI * 2f / period);
        for (int i = 0; i < clouds.Length; i++)
        {
            if (clouds[i] == null)
                continue;

            float start = i < startAngles.Length ? startAngles[i] * Mathf.Deg2Rad : 0f;
            float a = angle + start;
            clouds[i].anchoredPosition = orbitCenter + new Vector2(Mathf.Cos(a) * orbitRadius.x, Mathf.Sin(a) * orbitRadius.y);
        }
    }
}
