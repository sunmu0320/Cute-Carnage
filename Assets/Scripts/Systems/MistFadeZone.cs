using System.Collections.Generic;
using UnityEngine;

/// <summary>Thins mist puffs that drift into the ParticleSystem's trigger colliders (e.g. inside the
/// fence) down to keepFraction of their opacity, gradually instead of popping them. Requires the Trigger module's Inside action set to Callback.</summary>
[RequireComponent(typeof(ParticleSystem))]
public class MistFadeZone : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float fadeSeconds = 1.5f;
    [SerializeField, Range(0f, 1f)] private float keepFraction = 0.2f; // 0 = clear the zone completely

    private ParticleSystem ps;
    private readonly List<ParticleSystem.Particle> inside = new List<ParticleSystem.Particle>();

    private void Awake() => ps = GetComponent<ParticleSystem>();

    private void OnParticleTrigger()
    {
        int n = ps.GetTriggerParticles(ParticleSystemTriggerEventType.Inside, inside);
        float step = Time.deltaTime / fadeSeconds * 255f;
        float floor = keepFraction * 255f;
        for (int i = 0; i < n; i++)
        {
            var p = inside[i];
            Color32 c = p.startColor;
            if (c.a <= floor) continue;
            c.a = (byte)Mathf.Max(floor, c.a - Mathf.Max(1f, step));
            p.startColor = c;
            if (c.a == 0) p.remainingLifetime = 0f;
            inside[i] = p;
        }
        ps.SetTriggerParticles(ParticleSystemTriggerEventType.Inside, inside);
    }
}
