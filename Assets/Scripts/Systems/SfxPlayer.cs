using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One shared pool of positional AudioSources for all one-shot sounds, so hundreds of zombies can't pile up
/// hundreds of overlapping copies. Each clip has a minimum gap between plays, and at most MaxVoices sounds
/// play at once (extra requests are dropped). Created on first use in the active scene; master volume is
/// AudioListener.volume (SettingsPanelUI).
/// </summary>
public class SfxPlayer : MonoBehaviour
{
    private const int MaxVoices = 24;

    private static SfxPlayer instance;

    private readonly AudioSource[] voices = new AudioSource[MaxVoices];
    private readonly Dictionary<AudioClip, float> nextAllowed = new Dictionary<AudioClip, float>();
    private int cursor;

    /// <summary>Plays clip at position unless the same clip played less than minGap seconds ago or every
    /// voice is busy. Null clip = silent (sound not added yet).</summary>
    public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float minGap = 0.08f, float pitchJitter = 0.1f)
    {
        if (clip == null || Time.timeScale <= 0f)
        {
            return;
        }

        if (instance == null)
        {
            instance = new GameObject("SfxPlayer").AddComponent<SfxPlayer>();
        }

        instance.PlayInternal(clip, position, volume, minGap, pitchJitter);
    }

    /// <summary>Same as Play, with a random variant from clips (empty/null = silent).</summary>
    public static void Play(AudioClip[] clips, Vector3 position, float volume = 1f, float minGap = 0.08f, float pitchJitter = 0.1f)
    {
        if (clips != null && clips.Length > 0)
        {
            Play(clips[Random.Range(0, clips.Length)], position, volume, minGap, pitchJitter);
        }
    }

    private void Awake()
    {
        for (int i = 0; i < MaxVoices; i++)
        {
            var go = new GameObject("Voice" + i);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0.6f; // mostly positional, still audible from the high top-down camera
            s.minDistance = 8f;
            s.maxDistance = 45f;
            voices[i] = s;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void PlayInternal(AudioClip clip, Vector3 position, float volume, float minGap, float pitchJitter)
    {
        if (nextAllowed.TryGetValue(clip, out float t) && Time.time < t)
        {
            return;
        }

        for (int n = 0; n < MaxVoices; n++)
        {
            AudioSource s = voices[cursor];
            cursor = (cursor + 1) % MaxVoices;
            if (s.isPlaying)
            {
                continue;
            }

            nextAllowed[clip] = Time.time + minGap;
            s.transform.position = position;
            s.clip = clip;
            s.volume = volume;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.Play();
            return;
        }
    }
}
