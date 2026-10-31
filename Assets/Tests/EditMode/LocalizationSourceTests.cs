using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CaSentLeSoufre.Core;
using NUnit.Framework;

namespace CaSentLeSoufre.Tests
{
    /// <summary>
    /// Vérifie que le CSV source (Assets/Localization/Source/Game.csv) traduit toutes les clés que le moteur
    /// peut émettre, dans toutes les langues, avec les mêmes paramètres {0}, {1}...
    /// </summary>
    public class LocalizationSourceTests
    {
        private static readonly string[] ValidationKeys =
        {
            "validation.rules_missing", "validation.powers_missing",
            "validation.rank_invalid", "validation.rank_duplicate", "validation.power_duplicate",
            "validation.players_count", "validation.rounds_range", "validation.name_required",
            "validation.name_too_long", "validation.name_duplicate"
        };

        /// <summary>Clés utilisées par l'interface (menu de configuration).</summary>
        private static readonly string[] MenuKeys =
        {
            "game.title", "menu.section.rules", "menu.mode.label", "mode.Classic.description",
            "mode.Demon.description", "menu.you", "menu.name_placeholder", "menu.rounds.label",
            "menu.settings", "menu.back", "menu.quick.title", "menu.players.count", "menu.versus",
            "menu.fix_settings", "menu.section.profile", "menu.profile.name", "menu.section.opponents",
            "menu.opponents.hint", "menu.unused", "menu.autosave",
            "menu.section.powers", "menu.powers.hint", "menu.power.value", "menu.section.options",
            "option.four_of_a_kind.label", "option.four_of_a_kind.description", "option.jump_in.label",
            "option.jump_in.description", "option.initial_swap.label", "option.initial_swap.description",
            "menu.start", "menu.errors.title", "menu.language", "language.fr", "language.en",
            "table.round", "table.turn_of", "table.your_turn", "table.direction_reversed", "table.burned",
            "table.draw_pile", "table.discard_pile", "table.hand", "table.face_up", "table.face_down",
            "table.hand_count", "table.out", "table.swap_hint", "table.must_pick_up", "table.play_blind",
            "table.jump_in_hint", "table.click_again", "table.ready", "table.play", "table.pick_up", "table.quit",
            "table.log.played", "table.log.jumped_in", "table.log.picked_up", "table.log.blind",
            "table.log.blind_fail", "table.log.burned", "table.log.power", "table.log.forced_pickup",
            "table.log.finished", "table.log.stalemate", "table.round_over", "table.match_over",
            "table.finish_order", "table.last", "table.standings", "table.standing_row", "table.winner",
            "table.next_round", "table.back_to_menu",
            "menu.stats", "stats.filter.all", "stats.group.rounds", "stats.group.matches", "stats.col.player",
            "stats.col.played", "stats.col.wins", "stats.col.losses", "stats.human", "stats.ai", "stats.empty",
            "stats.hint", "stats.empty_rounds", "stats.empty_matches",
            "card.rankname.11", "card.rankname.12", "card.rankname.13", "card.rankname.14"
        };

        [Test]
        public void Csv_ContainsEveryKeyTheMenuUses()
        {
            var keys = LoadCsv().Skip(1).Select(r => r[0]).ToList();
            foreach (var key in MenuKeys) Assert.Contains(key, keys, key);
        }

        private static List<List<string>> LoadCsv()
        {
            string root = Environment.GetEnvironmentVariable("CSL_PROJECT_ROOT") ?? Directory.GetCurrentDirectory();
            string relative = Path.Combine("Assets", "Localization", "Source", "Game.csv");
            var dir = new DirectoryInfo(root);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, relative))) dir = dir.Parent;
            Assert.IsNotNull(dir, "Game.csv introuvable à partir de " + root);
            return ParseCsv(File.ReadAllText(Path.Combine(dir.FullName, relative)));
        }

        private static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else field.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(field.ToString());
                    field.Clear();
                    if (row.Count > 1 || row[0].Length > 0) rows.Add(row);
                    row = new List<string>();
                }
                else field.Append(c);
            }
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
            return rows;
        }

        [Test]
        public void Csv_IsWellFormed_NoEmptyCells_NoDuplicateKeys()
        {
            var rows = LoadCsv();
            CollectionAssert.AreEqual(new[] { "Key", "fr", "en" }, rows[0]);

            var keys = new HashSet<string>();
            foreach (var row in rows.Skip(1))
            {
                Assert.AreEqual(3, row.Count, $"Ligne mal formée : {string.Join(" | ", row)}");
                Assert.IsTrue(keys.Add(row[0]), $"Clé en double : {row[0]}");
                Assert.IsNotEmpty(row[1].Trim(), $"Traduction fr vide : {row[0]}");
                Assert.IsNotEmpty(row[2].Trim(), $"Traduction en vide : {row[0]}");
            }
        }

        [Test]
        public void Csv_ContainsEveryKeyTheEngineCanEmit()
        {
            var keys = LoadCsv().Skip(1).Select(r => r[0]).ToHashSet();

            foreach (var key in ValidationKeys) Assert.Contains(key, keys.ToList(), key);
            foreach (var p in PowerCatalog.All)
            {
                Assert.Contains(PowerCatalog.LabelKey(p), keys.ToList());
                Assert.Contains(PowerCatalog.DescriptionKey(p), keys.ToList());
            }
            for (int rank = 11; rank <= 14; rank++)
                Assert.Contains(new Card(rank, Suit.Spades).RankKey, keys.ToList());
            foreach (GameMode m in Enum.GetValues(typeof(GameMode))) Assert.Contains($"mode.{m}.label", keys.ToList());
            foreach (AiDifficulty d in Enum.GetValues(typeof(AiDifficulty)))
                Assert.Contains($"difficulty.{d}.label", keys.ToList());
        }

        [Test]
        public void Csv_FrenchAndEnglish_UseTheSamePlaceholders()
        {
            var placeholder = new Regex(@"\{\d+\}");
            foreach (var row in LoadCsv().Skip(1))
            {
                var fr = placeholder.Matches(row[1]).Select(m => m.Value).OrderBy(x => x).ToList();
                var en = placeholder.Matches(row[2]).Select(m => m.Value).OrderBy(x => x).ToList();
                CollectionAssert.AreEqual(fr, en, $"Paramètres différents pour {row[0]}");
            }
        }
    }
}
