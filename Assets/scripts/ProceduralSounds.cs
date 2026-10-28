using System.Collections.Generic;
using UnityEngine;

/// Sonidos generados por codigo (no hacen falta ficheros de audio). Se usan cuando el campo de sonido de un script
/// esta vacio: chapoteo del remo, siseo de arana, huesos, golpes, espada, cofre, campanitas de objetivo, olas...
/// No es un componente: ProceduralSounds.Play(ProceduralSounds.Splash(), posicion).
public static class ProceduralSounds
{
    const int Rate = 44100;
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static System.Random rng = new System.Random(7);

    /// Reproduce un sonido en un punto (3D a medias: se oye de donde viene, pero tambien de lejos)
    public static void Play(AudioClip clip, Vector3 pos, float volume = 1f, float spatial = 0.7f, float pitchJitter = 0.08f)
    {
        if (clip == null) return;
        var go = new GameObject("Sonido " + clip.name);
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = spatial;
        src.minDistance = 3f;
        src.maxDistance = 40f;
        src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        src.Play();
        Object.Destroy(go, clip.length / Mathf.Max(src.pitch, 0.1f) + 0.1f);
    }

    static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

    static AudioClip Make(string name, float seconds, System.Func<float, float> sample, bool loop = false)
    {
        if (cache.TryGetValue(name, out var c) && c != null) return c;
        int n = Mathf.Max(1, (int)(Rate * seconds));
        var data = new float[n];
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) { data[i] = sample(i / (float)Rate); peak = Mathf.Max(peak, Mathf.Abs(data[i])); }
        float norm = 0.9f / peak;
        for (int i = 0; i < n; i++) data[i] *= norm;
        if (loop)   // fundido en los extremos para que el bucle no haga 'clic'
            for (int i = 0; i < Rate / 10 && i < n / 2; i++) { float k = i / (Rate / 10f); data[i] *= k; data[n - 1 - i] *= k; }
        c = AudioClip.Create(name, n, 1, Rate, false);
        c.SetData(data, 0);
        cache[name] = c;
        return c;
    }

    // Ruido filtrado (paso bajo simple): 'smooth' 0..1, mas alto = mas grave
    static System.Func<float, float> Filtered(float smooth, System.Func<float, float> env)
    {
        float last = 0f;
        return t => { last = Mathf.Lerp(Noise(), last, smooth); return last * env(t); };
    }

    /// Chapoteo de la pala al entrar en el agua
    public static AudioClip Splash() => Make("Chapoteo", 0.5f, Filtered(0.55f, t => Mathf.Clamp01(t * 60f) * Mathf.Exp(-t * 9f)));

    /// Siseo de arana (ruido agudo que sube y baja)
    public static AudioClip Hiss() => Make("Siseo", 0.6f, Filtered(0.05f, t => Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.6f)) * (0.6f + 0.4f * Mathf.Sin(t * 70f))));

    /// Traqueteo de huesos (clics secos seguidos): esqueletos
    public static AudioClip Rattle() => Make("Huesos", 0.5f, t =>
    {
        float clickT = t % 0.045f;
        float amp = Mathf.Exp(-clickT * 300f) * (1f - t / 0.5f);
        return (Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.6f + Noise() * 0.5f) * amp;
    });

    /// Golpe sordo (coco, impacto, dano)
    public static AudioClip Thud() => Make("Golpe", 0.3f, t => (Mathf.Sin(2f * Mathf.PI * (110f - t * 150f) * t) + Noise() * 0.3f) * Mathf.Exp(-t * 18f));

    /// Zumbido de la espada al dar un tajo
    public static AudioClip Whoosh() => Make("Zumbido", 0.35f, Filtered(0.3f, t => Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.35f))));

    /// Golpe metalico de la espada contra un enemigo
    public static AudioClip Slash() => Make("Tajo", 0.4f, t => (Mathf.Sin(2f * Mathf.PI * 1250f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 2630f * t) * 0.3f + Noise() * 0.5f * Mathf.Exp(-t * 40f)) * Mathf.Exp(-t * 10f));

    /// Crujido de madera (cofre que se abre)
    public static AudioClip Creak() => Make("Crujido", 1.1f, t =>
    {
        float f = 90f + 60f * Mathf.Sin(t * 5f);
        float saw = (t * f) % 1f * 2f - 1f;
        return saw * (0.5f + 0.5f * Mathf.Sin(t * 37f)) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.1f)) * 0.7f;
    });

    /// Clonc de la cerradura que no gira (de dia)
    public static AudioClip Clunk() => Make("Clonc", 0.35f, t => (Mathf.Sin(2f * Mathf.PI * 320f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.4f) * Mathf.Exp(-t * 14f));

    /// Campanitas: 'up' = objetivo cumplido, victoria; si no, bajando (derrota)
    public static AudioClip Chime(bool up = true) => Make(up ? "Campanas" : "CampanasBaja", 1.4f, t =>
    {
        float[] notes = up ? new[] { 523.3f, 659.3f, 784f, 1046.5f } : new[] { 523.3f, 415.3f, 349.2f, 261.6f };
        float s = 0f;
        for (int k = 0; k < notes.Length; k++)
        {
            float st = t - k * 0.18f;
            if (st > 0f) s += Mathf.Sin(2f * Mathf.PI * notes[k] * st) * Mathf.Exp(-st * 3.5f);
        }
        return s;
    });

    /// Arena del reloj al darle la vuelta (siseo suave y largo)
    public static AudioClip Sand() => Make("Arena", 2.5f, Filtered(0.2f, t => Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 2.5f)) * 0.5f));

    /// Olas del mar (bucle de 8 s)
    public static AudioClip Waves() => Make("Olas", 8f, Filtered(0.92f, t => 0.35f + 0.65f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 4f), 2f)), true);
}
