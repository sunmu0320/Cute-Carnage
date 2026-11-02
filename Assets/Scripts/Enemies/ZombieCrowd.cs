using UnityEngine;

/// <summary>
/// SC2-style crowd spacing: once per frame (after every Zombie.Update) overlapping zombies are pushed apart.
/// Anchored zombies (attacking, emerging, frozen) never move, so walkers queue behind the front line and slide
/// into gaps on their own. Spatial hash on the XZ plane, preallocated arrays, no per-frame GC.
/// </summary>
public static class ZombieCrowd
{
    private const int BucketCount = 1024; // power of two
    private const float MaxPushPerFrame = 0.15f; // m; caps the pop when a spawn burst lands on one point
    // Sideways slide per metre of overlap with an attacker; ~1 ≈ walking pace along the front line.
    private const float SlideFactor = 1f;

    private static int lastFrame = -1;
    private static Zombie[] zombies = new Zombie[0];
    private static float[] x = new float[0], z = new float[0], radius = new float[0], dx = new float[0], dz = new float[0];
    private static float[] slideSign = new float[0];
    private static bool[] anchored = new bool[0], blocking = new bool[0];
    private static int[] next = new int[0];
    private static readonly int[] head = new int[BucketCount];

    /// <summary>Called from every Zombie.LateUpdate; only the first call in a frame does the work.</summary>
    public static void ResolveOncePerFrame()
    {
        if (lastFrame == Time.frameCount || Time.deltaTime <= 0f)
        {
            return;
        }

        lastFrame = Time.frameCount;
        Resolve();
    }

    private static void Resolve()
    {
        var active = Zombie.Active;
        EnsureCapacity(active.Count);

        int n = 0;
        float maxRadius = 0.05f;
        for (int i = 0; i < active.Count; i++)
        {
            Zombie zb = active[i];
            if (zb.IsDead)
            {
                continue; // corpses don't block
            }

            Vector3 p = zb.transform.position;
            zombies[n] = zb;
            x[n] = p.x;
            z[n] = p.z;
            radius[n] = zb.CrowdRadius;
            anchored[n] = zb.IsCrowdAnchored;
            slideSign[n] = zb.CrowdSlideSign;
            blocking[n] = anchored[n] || zb.IsCrowdWaiting;
            dx[n] = 0f;
            dz[n] = 0f;
            maxRadius = Mathf.Max(maxRadius, radius[n]);
            n++;
        }

        if (n < 2)
        {
            return;
        }

        // Cell = largest diameter, so any overlapping pair sits in neighbouring cells.
        float inv = 1f / (2f * maxRadius);
        System.Array.Fill(head, -1);
        for (int i = 0; i < n; i++)
        {
            int b = Bucket(Mathf.FloorToInt(x[i] * inv), Mathf.FloorToInt(z[i] * inv));
            next[i] = head[b];
            head[b] = i;
        }

        for (int i = 0; i < n; i++)
        {
            int cx = Mathf.FloorToInt(x[i] * inv);
            int cz = Mathf.FloorToInt(z[i] * inv);
            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oz = -1; oz <= 1; oz++)
                {
                    for (int j = head[Bucket(cx + ox, cz + oz)]; j >= 0; j = next[j])
                    {
                        // Each pair once; hash collisions just add a few extra (harmless) checks.
                        if (j <= i || (anchored[i] && anchored[j]))
                        {
                            continue;
                        }

                        PushApart(i, j);
                    }
                }
            }
        }

        for (int i = 0; i < n; i++)
        {
            float lenSq = dx[i] * dx[i] + dz[i] * dz[i];
            if (lenSq <= 1e-8f)
            {
                continue;
            }

            float scale = lenSq > MaxPushPerFrame * MaxPushPerFrame ? MaxPushPerFrame / Mathf.Sqrt(lenSq) : 1f;
            Transform t = zombies[i].transform;
            Vector3 p = t.position;
            p.x += dx[i] * scale;
            p.z += dz[i] * scale;
            t.position = p;
        }

        System.Array.Clear(zombies, 0, n); // don't keep destroyed zombies alive in the static array
    }

    private static void PushApart(int i, int j)
    {
        float ddx = x[j] - x[i];
        float ddz = z[j] - z[i];
        float minDist = radius[i] + radius[j];
        float distSq = ddx * ddx + ddz * ddz;
        if (distSq >= minDist * minDist)
        {
            return;
        }

        float dist = Mathf.Sqrt(distSq);
        if (dist < 1e-4f)
        {
            // Same spot (spawn burst): split along a per-pair direction so they don't stay stacked.
            float a = (i * 0.618f + j * 0.382f) * Mathf.PI * 2f;
            ddx = Mathf.Cos(a);
            ddz = Mathf.Sin(a);
            dist = 1f;
            distSq = 0f;
        }

        float overlap = minDist - Mathf.Sqrt(distSq);
        float nx = ddx / dist;
        float nz = ddz / dist;

        // Anchored side doesn't move; the walker takes the whole push. Two walkers split it.
        float shareI = anchored[i] ? 0f : anchored[j] ? 1f : 0.5f;
        float shareJ = 1f - shareI;
        dx[i] -= nx * overlap * shareI;
        dz[i] -= nz * overlap * shareI;
        dx[j] += nx * overlap * shareJ;
        dz[j] += nz * overlap * shareJ;

        // Walker blocked by an attacker or a stuck queue member: also slide around it (its own fixed side),
        // so every row drifts along the front line, wraps corners and steps into the first gap.
        if (!anchored[j] && blocking[i])
        {
            Slide(j, nx, nz, overlap);
        }

        if (!anchored[i] && blocking[j])
        {
            Slide(i, -nx, -nz, overlap);
        }
    }

    /// <summary>Tangential push for walker w; (nx, nz) points from the blocker to w.</summary>
    private static void Slide(int w, float nx, float nz, float overlap)
    {
        float s = slideSign[w] * overlap * SlideFactor;
        dx[w] -= nz * s;
        dz[w] += nx * s;
    }

    private static int Bucket(int cx, int cz)
    {
        return ((cx * 73856093) ^ (cz * 19349663)) & (BucketCount - 1);
    }

    private static void EnsureCapacity(int count)
    {
        if (zombies.Length >= count)
        {
            return;
        }

        int size = Mathf.Max(count, zombies.Length * 2, 64);
        zombies = new Zombie[size];
        x = new float[size];
        z = new float[size];
        radius = new float[size];
        dx = new float[size];
        dz = new float[size];
        anchored = new bool[size];
        blocking = new bool[size];
        slideSign = new float[size];
        next = new int[size];
    }
}
