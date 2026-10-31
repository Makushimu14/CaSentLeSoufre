using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Synthétise les effets sonores et la musique d'ambiance du jeu, en WAV 16 bits mono.
/// Tout est déterministe : relancer l'outil redonne exactement les mêmes fichiers.
/// </summary>
internal static class Program
{
    private const int Rate = 44100;
    private const int MusicRate = 22050;
    private const double Tau = Math.PI * 2;

    private static int Main()
    {
        string outDir = Path.Combine(FindProjectRoot(), "Assets", "Resources", "Sounds");
        Directory.CreateDirectory(outDir);

        // Nom, signal, fréquence d'échantillonnage, crête visée (0 à 1).
        var sounds = new List<(string name, float[] data, int rate, float peak)>
        {
            ("card_place", CardPlace(), Rate, 0.70f),
            ("card_slide", CardSlide(), Rate, 0.45f),
            ("card_deal", CardDeal(), Rate, 0.45f),
            ("pick_up", PickUp(), Rate, 0.60f),
            ("burn", Burn(), Rate, 0.70f),
            ("power", PowerChime(), Rate, 0.55f),
            ("jump_in", JumpIn(), Rate, 0.60f),
            ("your_turn", YourTurn(), Rate, 0.40f),
            ("win", Win(), Rate, 0.60f),
            ("lose", Lose(), Rate, 0.60f),
            ("round_end", RoundEnd(), Rate, 0.55f),
            ("click", Click(), Rate, 0.40f),
            ("select", Select(), Rate, 0.35f),
            ("ambient", Ambient(), MusicRate, 0.45f)
        };

        foreach (var (name, data, rate, peak) in sounds)
        {
            Normalize(data, peak);
            WriteWav(Path.Combine(outDir, name + ".wav"), data, rate);
            Console.WriteLine($"{name,-11} {data.Length / (double)rate,5:0.00} s   crête {PeakOf(data):0.00}   énergie {Rms(data):0.000}");
        }

        // Contrôle de la boucle d'ambiance : début et fin doivent se raccorder sans saut.
        var ambient = sounds[sounds.Count - 1].data;
        Console.WriteLine($"raccord de la boucle : {Math.Abs(ambient[0] - ambient[ambient.Length - 1]):0.0000}");
        Console.WriteLine($"{sounds.Count} sons écrits dans {outDir}");
        return 0;
    }

    private static string FindProjectRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Assets"))
                && Directory.Exists(Path.Combine(dir.FullName, "ProjectSettings")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Projet Unity introuvable : lancez la commande depuis le dossier du projet.");
    }

    // ---------------------------------------------------------------- Outils de synthèse

    private static float[] Buffer(double seconds, int rate = Rate) => new float[(int)Math.Round(seconds * rate)];

    private static float[] Noise(int length, int seed)
    {
        var rng = new Random(seed);
        var x = new float[length];
        for (int i = 0; i < length; i++) x[i] = (float)(rng.NextDouble() * 2 - 1);
        return x;
    }

    /// <summary>Filtre passe-bas à un pôle, fréquence de coupure variable dans le temps.</summary>
    private static float[] LowPass(float[] x, Func<double, double> cutoffHz, int rate = Rate)
    {
        var y = new float[x.Length];
        double state = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double fc = Math.Max(20, cutoffHz(i / (double)rate));
            double a = 1 - Math.Exp(-Tau * fc / rate);
            state += a * (x[i] - state);
            y[i] = (float)state;
        }
        return y;
    }

    private static float[] HighPass(float[] x, Func<double, double> cutoffHz, int rate = Rate)
    {
        var low = LowPass(x, cutoffHz, rate);
        var y = new float[x.Length];
        for (int i = 0; i < x.Length; i++) y[i] = x[i] - low[i];
        return y;
    }

    private static float[] BandPass(float[] x, Func<double, double> lowHz, Func<double, double> highHz, int rate = Rate) =>
        LowPass(HighPass(x, lowHz, rate), highHz, rate);

    private static void Envelope(float[] x, Func<double, double> gainAt, int rate = Rate)
    {
        for (int i = 0; i < x.Length; i++) x[i] *= (float)gainAt(i / (double)rate);
    }

    private static void Mix(float[] destination, float[] source, double atSeconds = 0, float gain = 1f, int rate = Rate)
    {
        int offset = (int)Math.Round(atSeconds * rate);
        for (int i = 0; i < source.Length && offset + i < destination.Length; i++)
            if (offset + i >= 0) destination[offset + i] += source[i] * gain;
    }

    /// <summary>Sinusoïde dont la fréquence et l'amplitude varient avec le temps.</summary>
    private static float[] Tone(double seconds, Func<double, double> freqHz, Func<double, double> amplitude, int rate = Rate)
    {
        var x = Buffer(seconds, rate);
        double phase = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double t = i / (double)rate;
            phase += Tau * freqHz(t) / rate;
            x[i] = (float)(Math.Sin(phase) * amplitude(t));
        }
        return x;
    }

    /// <summary>Son de cloche : plusieurs harmoniques qui s'éteignent à des vitesses différentes.</summary>
    private static float[] Bell(double seconds, double freq, double decay = 1.0, int rate = Rate)
    {
        double[] ratios = { 1.0, 2.0, 2.76, 5.4 };
        double[] gains = { 1.0, 0.5, 0.35, 0.15 };
        double[] taus = { 0.50, 0.35, 0.25, 0.12 };

        var x = Buffer(seconds, rate);
        for (int p = 0; p < ratios.Length; p++)
        {
            double step = Tau * freq * ratios[p] / rate;
            for (int i = 0; i < x.Length; i++)
            {
                double t = i / (double)rate;
                double attack = 1 - Math.Exp(-t / 0.003);
                x[i] += (float)(Math.Sin(step * i) * gains[p] * attack * Math.Exp(-t / (taus[p] * decay)));
            }
        }
        return x;
    }

    /// <summary>Note pincée : sinusoïde et deuxième harmonique qui s'éteignent vite.</summary>
    private static float[] Pluck(double seconds, double freq, double tau, int rate = Rate)
    {
        var x = Buffer(seconds, rate);
        for (int i = 0; i < x.Length; i++)
        {
            double t = i / (double)rate;
            double env = (1 - Math.Exp(-t / 0.002)) * Math.Exp(-t / tau);
            x[i] = (float)((Math.Sin(Tau * freq * t) + 0.4 * Math.Sin(Tau * freq * 2 * t)) * env);
        }
        return x;
    }

    private static void Normalize(float[] x, float peak)
    {
        float max = PeakOf(x);
        if (max <= 0) return;
        float scale = peak / max;
        for (int i = 0; i < x.Length; i++) x[i] *= scale;

        // Fondu très court aux deux bouts : jamais de claquement, sauf pour la boucle (elle se raccorde d'elle-même).
    }

    private static float PeakOf(float[] x)
    {
        float max = 0;
        foreach (float v in x)
        {
            if (float.IsNaN(v)) throw new InvalidOperationException("Valeur invalide dans un son.");
            max = Math.Max(max, Math.Abs(v));
        }
        return max;
    }

    private static double Rms(float[] x)
    {
        double sum = 0;
        foreach (float v in x) sum += v * v;
        return Math.Sqrt(sum / x.Length);
    }

    private static void WriteWav(string path, float[] samples, int rate)
    {
        using var stream = new FileStream(path, FileMode.Create);
        using var w = new BinaryWriter(stream);
        int dataBytes = samples.Length * 2;

        w.Write(new[] { 'R', 'I', 'F', 'F' });
        w.Write(36 + dataBytes);
        w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
        w.Write(16);
        w.Write((short)1);      // PCM
        w.Write((short)1);      // mono
        w.Write(rate);
        w.Write(rate * 2);      // octets par seconde
        w.Write((short)2);      // octets par échantillon
        w.Write((short)16);     // bits
        w.Write(new[] { 'd', 'a', 't', 'a' });
        w.Write(dataBytes);
        foreach (float v in samples)
            w.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, v)) * short.MaxValue));
    }

    // ---------------------------------------------------------------- Cartes

    /// <summary>Une carte posée sur la pile : un petit coup sourd et le claquement du papier.</summary>
    private static float[] CardPlace()
    {
        var x = Buffer(0.22);

        var thump = Tone(0.22, t => 150 - 80 * Math.Min(1, t / 0.06), t => Math.Exp(-t / 0.035) * 0.9);
        Mix(x, thump);

        var slap = BandPass(Noise(x.Length, 11), t => 1200, t => 5200);
        Envelope(slap, t => Math.Exp(-t / 0.018) * 0.8);
        Mix(x, slap);

        return x;
    }

    /// <summary>Une carte qui glisse : un souffle de bruit filtré qui monte puis redescend.</summary>
    private static float[] CardSlide()
    {
        double length = 0.3;
        var x = BandPass(Noise((int)(length * Rate), 21), t => 600 + 600 * (t / length), t => 1800 + 2400 * (t / length));
        Envelope(x, t =>
        {
            double s = Math.Sin(Math.PI * t / length);
            return s * s;
        });
        return x;
    }

    /// <summary>Une carte qui part en distribution : un petit claquement sec.</summary>
    private static float[] CardDeal()
    {
        var x = Buffer(0.09);
        var flick = HighPass(Noise(x.Length, 31), t => 2500);
        Envelope(flick, t => Math.Exp(-t / 0.012));
        Mix(x, flick, 0, 0.9f);
        Mix(x, Tone(0.09, t => 1800, t => Math.Exp(-t / 0.006) * 0.4));
        return x;
    }

    /// <summary>On ramasse la pile : un froissement qui s'éteint, suivi d'un petit choc sourd.</summary>
    private static float[] PickUp()
    {
        double length = 0.6;
        var x = LowPass(Noise((int)(length * Rate), 41), t => 2400 - 2000 * Math.Min(1, t / 0.45));
        Envelope(x, t =>
        {
            double swell = (1 - Math.Exp(-t / 0.04)) * Math.Exp(-t / 0.28);
            double rustle = 0.6 + 0.4 * Math.Sin(Tau * 17 * t); // froissement
            return swell * rustle;
        });
        Mix(x, Tone(length, t => 90, t => t > 0.32 ? Math.Exp(-(t - 0.32) / 0.06) * 0.5 : 0));
        return x;
    }

    // ---------------------------------------------------------------- Pouvoirs

    /// <summary>La pile brûle : un souffle qui monte, des craquements, un grondement grave.</summary>
    private static float[] Burn()
    {
        double length = 1.1;
        var x = Buffer(length);

        var whoosh = LowPass(Noise(x.Length, 51), t => 350 + 3600 * Math.Sin(Math.PI * Math.Min(1, t / 0.9)));
        Envelope(whoosh, t =>
        {
            double s = Math.Sin(Math.PI * Math.Min(1, t / length));
            return s * s * 0.9;
        });
        Mix(x, whoosh);

        var rng = new Random(52);
        for (int i = 0; i < 40; i++)
        {
            double at = 0.05 + rng.NextDouble() * 0.9;
            var pop = HighPass(Noise((int)(0.012 * Rate), 100 + i), t => 1800);
            Envelope(pop, t => Math.Exp(-t / 0.003));
            Mix(x, pop, at, (float)(0.4 + 0.6 * rng.NextDouble()));
        }

        Mix(x, Tone(length, t => 58, t => Math.Sin(Math.PI * Math.Min(1, t / length)) * 0.45));
        return x;
    }

    /// <summary>Un pouvoir se déclenche : une cloche claire.</summary>
    private static float[] PowerChime()
    {
        var x = Buffer(1.1);
        Mix(x, Bell(1.1, 660));
        Mix(x, Bell(1.1, 990), 0.05, 0.45f);
        return x;
    }

    /// <summary>On joue à la volée : trois notes vives qui montent.</summary>
    private static float[] JumpIn()
    {
        var x = Buffer(0.5);
        double[] notes = { 523.25, 659.25, 783.99 };
        for (int i = 0; i < notes.Length; i++) Mix(x, Pluck(0.4, notes[i], 0.12), i * 0.07);
        return x;
    }

    // ---------------------------------------------------------------- Partie

    /// <summary>C'est à toi : un petit « ping » doux.</summary>
    private static float[] YourTurn()
    {
        var x = Buffer(0.4);
        Mix(x, Tone(0.4, t => 880, t => (1 - Math.Exp(-t / 0.005)) * Math.Exp(-t / 0.1)));
        Mix(x, Tone(0.4, t => 1320, t => (1 - Math.Exp(-t / 0.005)) * Math.Exp(-t / 0.07) * 0.4));
        return x;
    }

    /// <summary>Victoire : arpège majeur qui monte, terminé par un accord.</summary>
    private static float[] Win()
    {
        var x = Buffer(2.0);
        double[] notes = { 523.25, 659.25, 783.99, 1046.5 };
        double[] times = { 0, 0.14, 0.28, 0.45 };
        for (int i = 0; i < notes.Length; i++) Mix(x, Bell(1.6, notes[i], 1.4), times[i], 0.7f);
        Mix(x, Bell(1.6, 659.25, 1.6), 0.45, 0.45f);
        Mix(x, Bell(1.6, 783.99, 1.6), 0.45, 0.45f);
        return x;
    }

    /// <summary>Défaite : notes qui descendent, plus sombres.</summary>
    private static float[] Lose()
    {
        var x = Buffer(2.0);
        double[] notes = { 392.0, 311.13, 261.63, 130.81 };
        double[] times = { 0, 0.28, 0.56, 0.9 };
        for (int i = 0; i < notes.Length; i++)
        {
            double freq = notes[i];
            var note = Tone(1.4, t => freq * (1 + 0.004 * Math.Sin(Tau * 5 * t)),
                t => (1 - Math.Exp(-t / 0.01)) * Math.Exp(-t / 0.45));
            var harmonic = Tone(1.4, t => freq * 2, t => (1 - Math.Exp(-t / 0.01)) * Math.Exp(-t / 0.3) * 0.3);
            Mix(x, note, times[i], 0.8f);
            Mix(x, harmonic, times[i], 0.8f);
        }
        return x;
    }

    /// <summary>Fin d'une manche (sans victoire ni défaite pour toi) : deux notes neutres.</summary>
    private static float[] RoundEnd()
    {
        var x = Buffer(1.2);
        Mix(x, Bell(1.0, 659.25, 1.1), 0, 0.8f);
        Mix(x, Bell(1.0, 493.88, 1.1), 0.2, 0.8f);
        return x;
    }

    // ---------------------------------------------------------------- Interface

    private static float[] Click() =>
        Tone(0.06, t => 1400 - 500 * Math.Min(1, t / 0.03), t => (1 - Math.Exp(-t / 0.001)) * Math.Exp(-t / 0.01));

    private static float[] Select() =>
        Tone(0.06, t => 700 - 200 * Math.Min(1, t / 0.03), t => (1 - Math.Exp(-t / 0.001)) * Math.Exp(-t / 0.012));

    // ---------------------------------------------------------------- Musique d'ambiance

    /// <summary>
    /// Ambiance sombre en boucle de 24 s : bourdon grave, nappe qui alterne deux accords, vent léger et cloches
    /// espacées. Les parties périodiques bouclent d'elles-mêmes ; le vent est raccordé par un fondu croisé et les
    /// cloches se prolongent de l'autre côté de la boucle.
    /// </summary>
    private static float[] Ambient()
    {
        const double length = 24.0;
        int n = (int)(length * MusicRate);
        var x = new float[n];

        // Bourdon : A1 et sa quinte, avec un léger battement ; tout fait un nombre entier de cycles dans la boucle.
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)MusicRate;
            double tremolo = 0.75 + 0.25 * Math.Sin(Tau * 2 * t / length);
            double drone = Math.Sin(Tau * 55.0 * t) * 0.34
                           + Math.Sin(Tau * 55.0 * (1 + 3.0 / (length * 55.0)) * t) * 0.18
                           + Math.Sin(Tau * 110.0 * t) * 0.16
                           + Math.Sin(Tau * 82.5 * t) * 0.14;
            x[i] += (float)(drone * tremolo);
        }

        // Nappe : deux accords (La mineur, Fa) qui se fondent l'un dans l'autre sur toute la boucle.
        double[][] chords =
        {
            new[] { 220.0, 261.63, 329.63 },
            new[] { 174.61, 220.0, 261.63 }
        };
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)MusicRate;
            double mixA = 0.5 + 0.5 * Math.Cos(Tau * t / length);
            double sample = 0;
            for (int c = 0; c < chords.Length; c++)
            {
                double weight = c == 0 ? mixA : 1 - mixA;
                foreach (double freq in chords[c])
                {
                    // Fréquences arrondies pour que la phase se raccorde en fin de boucle.
                    double f = Math.Round(freq * length) / length;
                    sample += weight * (Math.Sin(Tau * f * t) + 0.4 * Math.Sin(Tau * 2 * f * t) + 0.18 * Math.Sin(Tau * 3 * f * t));
                }
            }
            x[i] += (float)(sample * 0.045);
        }

        // Vent : bruit passe-bande, raccordé en fondu croisé.
        int fade = 2 * MusicRate;
        var windRaw = BandPass(Noise(n + fade, 71), t => 300, t => 900 + 500 * Math.Sin(Tau * t / length), MusicRate);
        for (int i = 0; i < n; i++)
        {
            float wind = windRaw[i];
            if (i < fade)
            {
                double w = i / (double)fade;
                wind = (float)(windRaw[i] * Math.Sin(w * Math.PI / 2) + windRaw[n + i] * Math.Cos(w * Math.PI / 2));
            }
            double t = i / (double)MusicRate;
            double gust = 0.6 + 0.4 * Math.Sin(Tau * 3 * t / length);
            x[i] += wind * (float)(0.5 * gust);
        }

        // Cloches rares, sur des notes sombres (La, Si bémol, Mi, Fa, Do) ; leur queue revient au début de la boucle.
        double[] bellNotes = { 440.0, 466.16, 659.25, 698.46, 523.25 };
        double[] bellTimes = { 3.0, 8.5, 13.0, 18.0, 21.5 };
        var rng = new Random(72);
        for (int b = 0; b < bellTimes.Length; b++)
        {
            double freq = bellNotes[rng.Next(bellNotes.Length)];
            var bell = Bell(7.0, freq, 5.0, MusicRate);
            int start = (int)(bellTimes[b] * MusicRate);
            for (int i = 0; i < bell.Length; i++) x[(start + i) % n] += bell[i] * 0.16f;
        }

        return x;
    }
}
