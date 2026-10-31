using System;
using System.IO;
using NUnit.Framework;

namespace CaSentLeSoufre.Tests
{
    /// <summary>
    /// Contrôle des fichiers sonores générés par Tools/SoundArt : bien formés, ni vides ni saturés, et tous
    /// présents sous les noms que le jeu utilise.
    /// </summary>
    public class SoundFilesTests
    {
        private static readonly string[] EffectNames =
        {
            "card_place", "card_slide", "card_deal", "pick_up", "burn", "power", "jump_in", "your_turn",
            "win", "lose", "round_end", "click", "select"
        };

        private const string MusicName = "ambient";

        private static string ProjectRoot()
        {
            string start = Environment.GetEnvironmentVariable("CSL_PROJECT_ROOT") ?? Directory.GetCurrentDirectory();
            var dir = new DirectoryInfo(start);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "Resources", "Sounds")))
                dir = dir.Parent;
            Assert.IsNotNull(dir, "Dossier Assets/Resources/Sounds introuvable à partir de " + start);
            return dir.FullName;
        }

        private sealed class WavInfo
        {
            public int Channels, SampleRate, Bits, DataBytes;
            public double Seconds;
            public double Peak;
        }

        private static WavInfo Read(string path)
        {
            using var reader = new BinaryReader(File.OpenRead(path));

            Assert.AreEqual("RIFF", new string(reader.ReadChars(4)), path);
            int riffSize = reader.ReadInt32();
            Assert.AreEqual("WAVE", new string(reader.ReadChars(4)), path);
            Assert.AreEqual("fmt ", new string(reader.ReadChars(4)), path);
            Assert.AreEqual(16, reader.ReadInt32(), "Format PCM de base");

            var info = new WavInfo();
            Assert.AreEqual(1, reader.ReadInt16(), "PCM");
            info.Channels = reader.ReadInt16();
            info.SampleRate = reader.ReadInt32();
            reader.ReadInt32(); // octets par seconde
            reader.ReadInt16(); // alignement
            info.Bits = reader.ReadInt16();

            Assert.AreEqual("data", new string(reader.ReadChars(4)), path);
            info.DataBytes = reader.ReadInt32();
            Assert.AreEqual(36 + info.DataBytes, riffSize, "Taille du fichier annoncée");
            Assert.AreEqual(info.DataBytes, reader.BaseStream.Length - 44, "Taille des données annoncée");

            int samples = info.DataBytes / 2;
            for (int i = 0; i < samples; i++)
                info.Peak = Math.Max(info.Peak, Math.Abs(reader.ReadInt16() / (double)short.MaxValue));
            info.Seconds = samples / (double)info.SampleRate;
            return info;
        }

        [Test]
        public void EveryEffect_IsAWellFormedMonoSound_NotEmptyAndNotClipping()
        {
            string folder = Path.Combine(ProjectRoot(), "Assets", "Resources", "Sounds");
            foreach (string name in EffectNames)
            {
                string path = Path.Combine(folder, name + ".wav");
                Assert.IsTrue(File.Exists(path), "Son manquant : " + name);

                var info = Read(path);
                Assert.AreEqual(1, info.Channels, name);
                Assert.AreEqual(16, info.Bits, name);
                Assert.AreEqual(44100, info.SampleRate, name);
                Assert.Greater(info.Seconds, 0.03, name + " : trop court");
                Assert.Less(info.Seconds, 3.0, name + " : trop long pour un effet");
                Assert.Greater(info.Peak, 0.2, name + " : presque silencieux");
                Assert.Less(info.Peak, 0.95, name + " : saturé");
            }
        }

        [Test]
        public void Music_IsALongLoopOfAboutTwentyFourSeconds_AtAModestLevel()
        {
            var info = Read(Path.Combine(ProjectRoot(), "Assets", "Resources", "Sounds", MusicName + ".wav"));

            Assert.AreEqual(1, info.Channels);
            Assert.AreEqual(16, info.Bits);
            Assert.AreEqual(22050, info.SampleRate);
            Assert.AreEqual(24.0, info.Seconds, 0.05);
            Assert.Greater(info.Peak, 0.2);
            Assert.Less(info.Peak, 0.7, "La musique reste discrète");
        }

        [Test]
        public void TheGame_UsesExactlyTheseSoundFiles()
        {
            string source = File.ReadAllText(Path.Combine(ProjectRoot(), "Assets", "Scripts", "Game", "AudioManager.cs"));
            foreach (string name in EffectNames)
                StringAssert.Contains("\"" + name + "\"", source, "AudioManager ne connaît pas " + name);
            StringAssert.Contains("\"" + MusicName + "\"", source);

            // Et aucun fichier orphelin dans le dossier.
            string folder = Path.Combine(ProjectRoot(), "Assets", "Resources", "Sounds");
            int expected = EffectNames.Length + 1;
            Assert.AreEqual(expected, Directory.GetFiles(folder, "*.wav").Length, "Fichiers .wav en trop ou en moins");
        }
    }
}
