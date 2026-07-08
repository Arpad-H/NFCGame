using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools > Audio > Loudness Baker.
//
// Measures every AudioClip in the chosen folders and bakes a per-clip gain into an
// AudioLoudnessTable, so clips of wildly different levels all play back at the same
// perceived loudness. Nothing is written to the audio files themselves — the gain is
// applied at playback time by AudioManager.
public class AudioLoudnessBakerWindow : EditorWindow
{
    private class Row
    {
        public AudioClip Clip;
        public string Error;
        public float Lufs;
        public float Peak;
        public float GainDb;
        public float AudibleLength;
        public bool PeakLimited;    // Gain was cut so the clip wouldn't clip the mix.
        public bool BoostClamped;   // Clip was too quiet to reach the target within MaxBoostDb.
    }

    private AudioLoudnessTable table;
    private LoudnessAnalyzer.Metric metric = LoudnessAnalyzer.Metric.Integrated;
    private readonly List<Object> folders = new List<Object> { null };
    private readonly List<Row> rows = new List<Row>();
    private Vector2 scroll;

    [MenuItem("Tools/Audio/Loudness Baker")]
    private static void Open() => GetWindow<AudioLoudnessBakerWindow>("Loudness Baker");

    private void OnGUI()
    {
        EditorGUILayout.Space();
        table = (AudioLoudnessTable)EditorGUILayout.ObjectField("Table", table, typeof(AudioLoudnessTable), false);

        if (table == null)
        {
            EditorGUILayout.HelpBox(
                "Create an AudioLoudnessTable (Assets > Create > Audio > Loudness Table) and assign it here.\n\n" +
                "Put it in a Resources folder, or assign it to AudioManager's Loudness Table field.",
                MessageType.Info);
            return;
        }

        DrawTargetSettings();
        DrawFolders();

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Analyze")) Analyze();

            using (new EditorGUI.DisabledScope(rows.Count == 0))
            {
                if (GUILayout.Button("Bake to Table")) Bake();
            }
        }

        DrawResults();
    }

    private void DrawTargetSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

        LoudnessAnalyzer.Metric previousMetric = metric;
        metric = (LoudnessAnalyzer.Metric)EditorGUILayout.EnumPopup(
            new GUIContent("Metric", "Integrated matches overall loudness. Max Momentary matches the " +
                                     "loudest 400 ms, which often feels better for short impact SFX."),
            metric);

        // The metric changes what's measured, not just how it's scaled, so the old rows are junk.
        if (metric != previousMetric) rows.Clear();

        EditorGUI.BeginChangeCheck();

        SerializedObject serialized = new SerializedObject(table);
        EditorGUILayout.PropertyField(serialized.FindProperty("TargetLufs"));
        EditorGUILayout.PropertyField(serialized.FindProperty("PeakCeilingDb"));
        EditorGUILayout.PropertyField(serialized.FindProperty("MaxBoostDb"));
        EditorGUILayout.PropertyField(serialized.FindProperty("MonoCompensationDb"));
        EditorGUILayout.PropertyField(serialized.FindProperty("SilenceFloorDb"));
        serialized.ApplyModifiedProperties();

        // These only scale the measurement, so recompute the gains in place rather than
        // forcing a re-analyze. SilenceFloorDb is the exception — it feeds the measured
        // audible length — but that's cheap enough to leave until the next Analyze.
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Row row in rows) ComputeGain(row);
        }
    }

    private void DrawFolders()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Folders to scan", EditorStyles.boldLabel);

        for (int i = 0; i < folders.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                folders[i] = EditorGUILayout.ObjectField(folders[i], typeof(DefaultAsset), false);

                if (GUILayout.Button("-", GUILayout.Width(24)) && folders.Count > 1)
                {
                    folders.RemoveAt(i);
                    return;
                }
            }
        }

        if (GUILayout.Button("Add folder")) folders.Add(null);

        EditorGUILayout.HelpBox("Leave empty to analyze the AudioClips currently selected in the Project window.",
            MessageType.None);
    }

    private void DrawResults()
    {
        if (rows.Count == 0) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"{rows.Count} clips", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Row row in rows)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(row.Clip, typeof(AudioClip), false, GUILayout.Width(180));

                if (row.Error != null)
                {
                    EditorGUILayout.LabelField($"failed: {row.Error}", EditorStyles.miniLabel);
                    continue;
                }

                EditorGUILayout.LabelField($"{row.Lufs,7:0.0} LUFS", GUILayout.Width(90));
                EditorGUILayout.LabelField($"{row.GainDb,6:+0.0;-0.0} dB", GUILayout.Width(70));
                EditorGUILayout.LabelField($"{row.AudibleLength:0.00}s / {row.Clip.length:0.00}s", GUILayout.Width(100));

                if (row.PeakLimited) EditorGUILayout.LabelField("peak limited", EditorStyles.miniLabel, GUILayout.Width(80));
                else if (row.BoostClamped) EditorGUILayout.LabelField("too quiet", EditorStyles.miniLabel, GUILayout.Width(80));
                else GUILayout.Space(80);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void Analyze()
    {
        rows.Clear();

        List<AudioClip> clips = CollectClips();
        if (clips.Count == 0)
        {
            EditorUtility.DisplayDialog("Loudness Baker",
                "No AudioClips found. Pick a folder, or select clips in the Project window.", "OK");
            return;
        }

        try
        {
            for (int i = 0; i < clips.Count; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Analyzing loudness",
                        clips[i].name, (i + 1) / (float)clips.Count))
                {
                    break;
                }

                rows.Add(AnalyzeClip(clips[i]));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        rows.Sort((a, b) => a.Clip.name.CompareTo(b.Clip.name));
    }

    private List<AudioClip> CollectClips()
    {
        List<string> searchPaths = new List<string>();
        foreach (Object folder in folders)
        {
            if (folder == null) continue;

            string path = AssetDatabase.GetAssetPath(folder);
            if (AssetDatabase.IsValidFolder(path)) searchPaths.Add(path);
        }

        List<AudioClip> clips = new List<AudioClip>();

        if (searchPaths.Count == 0)
        {
            foreach (Object selected in Selection.GetFiltered(typeof(AudioClip), SelectionMode.DeepAssets))
            {
                clips.Add((AudioClip)selected);
            }

            return clips;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", searchPaths.ToArray()))
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null) clips.Add(clip);
        }

        return clips;
    }

    // AudioClip.GetData only returns real samples for clips imported as Decompress On Load.
    // Anything else hands back a buffer of zeros, so flip the import setting, read, restore.
    private Row AnalyzeClip(AudioClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;

        AudioImporterSampleSettings original = default;
        bool reimported = false;

        if (importer != null)
        {
            original = importer.defaultSampleSettings;
            if (original.loadType != AudioClipLoadType.DecompressOnLoad || !original.preloadAudioData)
            {
                AudioImporterSampleSettings temporary = original;
                temporary.loadType = AudioClipLoadType.DecompressOnLoad;
                temporary.preloadAudioData = true;

                importer.defaultSampleSettings = temporary;
                importer.SaveAndReimport();
                reimported = true;
            }
        }

        LoudnessAnalyzer.Result result;
        try
        {
            // Reimporting invalidates the clip we were handed, so fetch it again.
            AudioClip loaded = Reload(path, clip);
            loaded.LoadAudioData();
            result = LoudnessAnalyzer.Analyze(loaded, metric, table.SilenceFloorDb);
        }
        finally
        {
            if (reimported)
            {
                AudioImporter restored = AssetImporter.GetAtPath(path) as AudioImporter;
                if (restored != null)
                {
                    restored.defaultSampleSettings = original;
                    restored.SaveAndReimport();
                }
            }
        }

        Row row = new Row { Clip = Reload(path, clip) };

        if (!result.Valid)
        {
            row.Error = result.Error;
            return row;
        }

        row.Lufs = result.Lufs;
        row.Peak = result.Peak;
        row.AudibleLength = result.AudibleLength;
        ComputeGain(row);

        return row;
    }

    // Re-fetches a clip whose native object a reimport just replaced. Deliberately not
    // "?? fallback": ?? bypasses UnityEngine.Object's == overload, so a destroyed clip
    // would come back as non-null.
    private static AudioClip Reload(string path, AudioClip fallback)
    {
        AudioClip loaded = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        return loaded != null ? loaded : fallback;
    }

    private void ComputeGain(Row row)
    {
        if (row.Error != null) return;

        row.PeakLimited = false;
        row.BoostClamped = false;

        float monoCompensation = row.Clip.channels == 1 ? table.MonoCompensationDb : 0f;
        float desiredDb = table.TargetLufs - row.Lufs + monoCompensation;

        if (desiredDb > table.MaxBoostDb)
        {
            desiredDb = table.MaxBoostDb;
            row.BoostClamped = true;
        }

        float gain = Mathf.Pow(10f, desiredDb / 20f);

        // Never let a normalised clip push its own peak past the ceiling.
        float ceiling = Mathf.Pow(10f, table.PeakCeilingDb / 20f);
        if (row.Peak * gain > ceiling)
        {
            gain = ceiling / row.Peak;
            row.PeakLimited = true;
            row.BoostClamped = false;
        }

        row.GainDb = 20f * Mathf.Log10(gain);
    }

    private void Bake()
    {
        List<AudioLoudnessTable.Entry> entries = new List<AudioLoudnessTable.Entry>();
        int skipped = 0;

        foreach (Row row in rows)
        {
            if (row.Error != null)
            {
                skipped++;
                continue;
            }

            entries.Add(new AudioLoudnessTable.Entry
            {
                Clip = row.Clip,
                Gain = Mathf.Pow(10f, row.GainDb / 20f),
                MeasuredLufs = row.Lufs,
                PlaybackLength = row.AudibleLength,
            });
        }

        Undo.RecordObject(table, "Bake Loudness Table");
        table.SetEntries(entries);
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();

        Debug.Log($"Loudness Baker: baked {entries.Count} clips into {table.name}" +
                  (skipped > 0 ? $", skipped {skipped} that could not be read." : "."), table);
    }
}
