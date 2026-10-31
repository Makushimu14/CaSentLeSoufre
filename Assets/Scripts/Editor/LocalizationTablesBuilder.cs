using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace CaSentLeSoufre.EditorTools
{
    /// <summary>
    /// Génère (ou met à jour) les langues et la table de traduction « Game » à partir de
    /// Assets/Localization/Source/Game.csv, qui reste la source de vérité : colonnes Key, fr, en.
    /// Relancer le menu après chaque modification du CSV ; les clés existantes sont mises à jour.
    /// </summary>
    public static class LocalizationTablesBuilder
    {
        public const string TableName = "Game";
        private const string CsvPath = "Assets/Localization/Source/Game.csv";
        private const string LocalesFolder = "Assets/Localization/Locales";
        private const string TablesFolder = "Assets/Localization/Tables";
        private const string SettingsPath = "Assets/Localization/Localization Settings.asset";

        [MenuItem("CaSentLeSoufre/Localisation/Générer les tables depuis le CSV")]
        public static void Build()
        {
            try
            {
                EnsureFolder("Assets/Localization");
                EnsureFolder(LocalesFolder);
                EnsureFolder(TablesFolder);

                var rows = ParseCsv(File.ReadAllText(CsvPath, Encoding.UTF8));
                if (rows.Count < 2) throw new InvalidDataException("Le CSV est vide.");

                // En-tête : Key, puis un code de langue par colonne (fr, en...).
                var header = rows[0];
                var codes = header.Skip(1).Select(c => c.Trim()).Where(c => c.Length > 0).ToList();

                EnsureSettings();
                var locales = codes.Select(EnsureLocale).ToList();

                var collection = LocalizationEditorSettings.GetStringTableCollection(TableName)
                                 ?? LocalizationEditorSettings.CreateStringTableCollection(
                                     TableName, TablesFolder, locales);

                int added = 0, updated = 0;
                foreach (var row in rows.Skip(1))
                {
                    if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0])) continue;
                    string key = row[0].Trim();

                    if (collection.SharedData.GetEntry(key) == null) collection.SharedData.AddKey(key);

                    for (int i = 0; i < codes.Count; i++)
                    {
                        string value = i + 1 < row.Count ? row[i + 1] : string.Empty;
                        var table = collection.GetTable(codes[i]) as StringTable;
                        if (table == null)
                        {
                            Debug.LogWarning($"[Localisation] Table « {TableName} » introuvable pour « {codes[i]} ».");
                            continue;
                        }

                        var entry = table.GetEntry(key);
                        if (entry == null)
                        {
                            table.AddEntry(key, value);
                            added++;
                        }
                        else if (entry.Value != value)
                        {
                            entry.Value = value;
                            updated++;
                        }
                    }
                }

                foreach (var t in collection.StringTables) EditorUtility.SetDirty(t);
                EditorUtility.SetDirty(collection.SharedData);
                EditorUtility.SetDirty(collection);
                AssetDatabase.SaveAssets();

                Debug.Log($"[Localisation] Table « {TableName} » à jour : {added} ajout(s), {updated} modification(s), " +
                          $"langues : {string.Join(", ", codes)}.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Localisation] Échec de la génération des tables : {e}");
            }
        }

        private static void EnsureSettings()
        {
            if (LocalizationEditorSettings.ActiveLocalizationSettings != null) return;

            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }

        private static Locale EnsureLocale(string code)
        {
            var existing = LocalizationEditorSettings.GetLocales().FirstOrDefault(l => l.Identifier.Code == code);
            if (existing != null) return existing;

            var locale = Locale.CreateLocale(new LocaleIdentifier(code));
            AssetDatabase.CreateAsset(locale, $"{LocalesFolder}/{code}.asset");
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Lecteur CSV minimal : virgules, guillemets doublés, retours à la ligne dans les champs entre guillemets.</summary>
        public static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else inQuotes = false;
                    }
                    else field.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',')
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
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

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }
            return rows;
        }
    }
}
