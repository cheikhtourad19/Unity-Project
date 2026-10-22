using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HeritageXR;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARSubsystems;

namespace HeritageXR.Editor
{
    // Content-only import. The accepted scene, cameras, UI and viewer are retained.
    public static class CollectionImport
    {
        const string Root = "Assets/HeritageXR";
        const string CatalogPath = "Content/catalog.json";
        static string CollectionPath => Root + "/Content/Collection.asset";
        static string LibraryPath => Root + "/Settings/ArtworkImages.asset";

        public static string Sync()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Content import requires Edit Mode.");
            var catalog = JObject.Parse(File.ReadAllText(CatalogPath));
            if ((int)catalog["schemaVersion"] != 2) throw new Exception("Expected catalogue schemaVersion 2.");
            var records = ((JArray)catalog["artworks"]).OrderBy(a => (int)a["sortOrder"]).ToArray();
            if (records.Length == 0 || records.Select(a => (string)a["id"]).Distinct().Count() != records.Length) throw new Exception("Empty catalogue or duplicate artwork ids.");
            foreach (var record in records) ValidateSources(record);
            var quizzes = records.SelectMany(a => ((JArray)a["games"]).Where(g => (string)g["kind"] == "quiz")).ToArray();
            if (quizzes.Select(g => (string)g["id"]).Distinct().Count() != quizzes.Length) throw new Exception("Duplicate quiz ids.");
            var questions = quizzes.SelectMany(g => ((JArray)g["questions"]).Select(q => (string)q["id"])).ToArray();
            if (questions.Distinct().Count() != questions.Length) throw new Exception("Duplicate question ids.");
            Directory.CreateDirectory(Root + "/Audio");
            AssetDatabase.Refresh();
            var collection = AssetDatabase.LoadAssetAtPath<MuseumCollection>(CollectionPath);
            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
            if (collection == null || library == null) throw new Exception("Existing collection/library must be preserved.");
            var imported = new List<MuseumArtwork>();
            var localized = new JArray();
            while (library.count > 0) library.RemoveAt(library.count - 1);
            foreach (var record in records)
            {
                string id = (string)record["id"];
                var texts = ((JArray)record["texts"]).ToDictionary(t => (string)t["language"]);
                var target = ((JArray)record["targets"]).First(t => (string)t["id"] == (string)record["coverTargetId"]);
                string photoPath = Root + "/Photos/" + id + ".jpg";
                Copy((string)target["path"], photoPath);
                ConfigureTexture(photoPath, 2048);
                var photograph = AssetDatabase.LoadAssetAtPath<Texture2D>(photoPath);
                float width = (float?)target["physicalWidthMeters"] ?? .12f;
                string prefabPath = Root + "/Models/" + id + "/Artwork.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null || prefab.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterials.Any(m => m == null || m.mainTexture == null)))
                    prefab = ImportModel(id, record["model"], prefabPath);
                var fr = texts["fr"];
                var artwork = new MuseumArtwork
                {
                    id = id, museumObjectId = (string)record["museumObjectId"],
                    title = (string)fr["title"], summary = (string)fr["summary"], description = (string)fr["description"],
                    photograph = photograph, modelPrefab = prefab, targetWidthMeters = width,
                    photoCredit = (string)target["author"], photoLicence = (string)target["licence"], photoSource = (string)target["sourcePage"],
                    modelLicence = (string)record["model"]["licence"], modelSource = (string)record["model"]["sourceRepository"],
                    modelCredits = record["model"]["credits"].Values<string>().ToArray(),
                    descriptionSources = texts.Values.SelectMany(t => t["sources"].Values<string>()).Distinct().ToArray()
                };
                artwork.quiz = ImportQuiz(record);
                foreach (var track in (JArray)record["audio"])
                {
                    string kind = (string)track["kind"], language = (string)track["language"];
                    string path = Root + "/Audio/" + id + "/" + kind + "." + language + ".mp3";
                    Copy((string)track["sourceAudioPath"], path);
                    ConfigureAudio(path);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null || clip.length <= 0) throw new Exception("Audio import failed: " + path);
                    var audioTrack = new MuseumAudioTrack { id = (string)track["id"], language = language, kind = kind, transcript = File.ReadAllText((string)track["scriptPath"]).Trim(), voiceId = (string)track["voiceId"], modelId = (string)track["generationModelId"], clip = clip };
                    audioTrack.subtitles = ImportSubtitles(track, clip);
                    artwork.audio.Add(audioTrack);
                    track["unityAudioPath"] = path;
                }
                imported.Add(artwork);
                var entry = new JObject { ["id"] = id };
                foreach (var language in new[] { "fr", "ar", "en" })
                    entry[language] = new JObject { ["title"] = (string)texts[language]["title"], ["summary"] = (string)texts[language]["summary"], ["description"] = (string)texts[language]["description"] };
                localized.Add(entry);
                int index = library.count;
                library.Add(); library.SetName(index, id); library.SetTexture(index, photograph, true);
                library.SetSpecifySize(index, true); library.SetSize(index, new Vector2(width, width * (int)target["pixelHeight"] / (int)target["pixelWidth"]));
                target["physicalWidthMeters"] = width;
                target["dimensionStatus"] = "prototype_print_specification_not_measured";
                target["unityTexturePath"] = photoPath; target["referenceLibraryPath"] = LibraryPath;
                target["preparationStatus"] = "image_imported_existing_prototype";
                record["model"]["unityPrefabPath"] = prefabPath; record["model"]["importStatus"] = "imported_in_unity_prototype";
                record["readiness"]["modelImported"] = true;
            }
            collection.artworks = imported;
            EditorUtility.SetDirty(collection); EditorUtility.SetDirty(library);
            File.WriteAllText(Root + "/Resources/Localization/ArtworkTexts.json", new JObject { ["artworks"] = localized }.ToString() + "\n");
            AssetDatabase.ImportAsset(Root + "/Resources/Localization/ArtworkTexts.json", ImportAssetOptions.ForceSynchronousImport);
            AttachToExistingScene(collection);
            catalog["status"] = "integrated_prototype_pending_device_validation";
            File.WriteAllText(CatalogPath, catalog.ToString() + "\n");
            AssetDatabase.SaveAssets();
            return Verify();
        }

        static void ValidateSources(JToken record)
        {
            var id = (string)record["id"];
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z0-9_]+$")) throw new Exception("Invalid artwork id.");
            var texts = ((JArray)record["texts"]).Select(t => (string)t["language"]).OrderBy(t => t).ToArray();
            if (!texts.SequenceEqual(new[] { "ar", "en", "fr" })) throw new Exception("Missing localized text: " + id);
            var audio = (JArray)record["audio"];
            var expected = new[] { "fr", "ar", "en" }.SelectMany(l => new[] { l + ".narration", l + ".audio_description" }).OrderBy(x => x);
            if (!audio.Select(t => (string)t["language"] + "." + (string)t["kind"]).OrderBy(x => x).SequenceEqual(expected)) throw new Exception("Missing audio entry: " + id);
            foreach (var track in audio)
            {
                string script = (string)track["scriptPath"], sound = (string)track["sourceAudioPath"];
                if (string.IsNullOrEmpty(sound) || !File.Exists(sound) || !File.Exists(script)) throw new Exception("Missing audio/script: " + track["id"]);
                using var sha = SHA256.Create();
                string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(script))).Replace("-", "").ToLowerInvariant();
                if (hash != (string)track["scriptSha256"]) throw new Exception("Script checksum changed: " + script);
            }
            if (!File.Exists((string)record["model"]["sourcePath"])) throw new Exception("Missing source model: " + id);
            foreach (var target in (JArray)record["targets"])
                if (!File.Exists((string)target["path"])) throw new Exception("Missing photo: " + id);
        }

        static MuseumQuiz ImportQuiz(JToken record)
        {
            var games = ((JArray)record["games"]).Where(g => (string)g["kind"] == "quiz").ToArray();
            if (games.Length == 0) return null;
            if (games.Length > 1) throw new Exception("Only one quiz per artwork is currently supported: " + record["id"]);
            var game = games[0];
            var quiz = new MuseumQuiz { id = (string)game["id"], contentVersion = (int)game["contentVersion"] };
            if (string.IsNullOrWhiteSpace(quiz.id) || quiz.contentVersion < 1 || ((JArray)game["questions"]).Count == 0) throw new Exception("Invalid quiz metadata.");
            foreach (var item in (JArray)game["questions"])
            {
                var question = new MuseumQuizQuestion { id = (string)item["id"], correctOptionId = (string)item["correctOptionId"], prompt = QuizText(item["prompt"]), explanation = QuizText(item["explanation"]) };
                if (string.IsNullOrWhiteSpace(question.id) || !File.Exists((string)item["sourceScriptPath"]) || !item["sources"].Any()) throw new Exception("Missing question provenance: " + question.id);
                foreach (var option in (JArray)item["options"]) question.options.Add(new MuseumQuizOption { id = (string)option["id"], text = QuizText(option["text"]) });
                if (question.options.Count != 4 || question.options.Any(o => string.IsNullOrWhiteSpace(o.id)) || question.options.Select(o => o.id).Distinct().Count() != 4 || question.options.Count(o => o.id == question.correctOptionId) != 1) throw new Exception("Invalid quiz choices: " + question.id);
                foreach (string language in new[] { "fr", "ar", "en" }) if (question.options.Select(o => o.text.In(language)).Distinct().Count() != 4) throw new Exception("Duplicate choice wording: " + question.id);
                quiz.questions.Add(question);
            }
            return quiz;
        }
        static MuseumQuizText QuizText(JToken text)
        {
            var value = new MuseumQuizText { fr = (string)text["fr"], ar = (string)text["ar"], en = (string)text["en"] };
            if (string.IsNullOrWhiteSpace(value.fr) || string.IsNullOrWhiteSpace(value.ar) || string.IsNullOrWhiteSpace(value.en)) throw new Exception("Missing quiz translation.");
            return value;
        }

        static List<MuseumSubtitleCue> ImportSubtitles(JToken track, AudioClip clip)
        {
            var result = new List<MuseumSubtitleCue>();
            if (track["subtitlesPath"].Type == JTokenType.Null) return result;
            string path = (string)track["subtitlesPath"];
            var document = JObject.Parse(File.ReadAllText(path));
            if ((string)document["trackId"] != (string)track["id"] || (string)document["scriptSha256"] != (string)track["scriptSha256"])
                throw new Exception("Subtitle identity/script mismatch: " + path);
            using var sha = SHA256.Create();
            string audioHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes((string)track["sourceAudioPath"]))).Replace("-", "").ToLowerInvariant();
            if (audioHash != (string)document["audioSha256"]) throw new Exception("Subtitles belong to a different recording: " + path);
            if (Math.Abs(clip.length - (double)document["durationSeconds"]) > .15) throw new Exception("Audio duration changed after import: " + path);
            float end = 0;
            var segments = (JArray)document["segments"];
            if (segments.Count == 0 || !JToken.DeepEquals(segments, track["subtitleSegments"])) throw new Exception("Catalogue/subtitle file mismatch: " + path);
            foreach (var cue in segments)
            {
                float start = (float)cue["startSeconds"], stop = (float)cue["endSeconds"];
                string text = (string)cue["text"];
                if (start < end || stop <= start || stop > clip.length + .05 || string.IsNullOrWhiteSpace(text))
                    throw new Exception("Invalid subtitle interval: " + path);
                result.Add(new MuseumSubtitleCue { startSeconds = start, endSeconds = stop, text = text }); end = stop;
            }
            string displayed = System.Text.RegularExpressions.Regex.Replace(string.Join(" ", result.Select(c => c.text)), "\\s+", " ").Trim();
            string script = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText((string)track["scriptPath"]), "\\s+", " ").Trim();
            if (displayed != script) throw new Exception("Subtitle text differs from the script: " + path);
            return result;
        }

        static void Copy(string from, string to)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            if (!File.Exists(to) || !File.ReadAllBytes(from).SequenceEqual(File.ReadAllBytes(to))) File.Copy(from, to, true);
            AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceSynchronousImport);
        }
        static void ConfigureTexture(string path, int size)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default; importer.maxTextureSize = size;
            importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Compressed; importer.SaveAndReimport();
        }
        static void ConfigureAudio(string path)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .8f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings; importer.SaveAndReimport();
        }
        static GameObject ImportModel(string id, JToken metadata, string prefabPath)
        {
            string folder = Path.GetDirectoryName(prefabPath);
            foreach (var source in metadata["materialPaths"].Values<string>().Concat(metadata["texturePaths"].Values<string>())) Copy(source, folder + "/" + Path.GetFileName(source));
            foreach (var source in metadata["texturePaths"].Values<string>()) ConfigureTexture(folder + "/" + Path.GetFileName(source), 2048);
            string objPath = folder + "/" + Path.GetFileName((string)metadata["sourcePath"]);
            Copy((string)metadata["sourcePath"], objPath);
            var importer = (ModelImporter)AssetImporter.GetAtPath(objPath);
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.importAnimation = false; importer.isReadable = false; importer.SaveAndReimport();
            var materials = new Dictionary<string, Material>();
            foreach (string mtl in metadata["materialPaths"].Values<string>())
            {
                string name = null;
                foreach (string raw in File.ReadLines(mtl))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("newmtl ")) name = line.Substring(7).Trim();
                    if (!line.StartsWith("map_Kd ") || name == null) continue;
                    string path = folder + "/" + name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
                    material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + Path.GetFileName(line.Substring(7).Trim()));
                    if (material.mainTexture == null) throw new Exception("Missing texture for " + name);
                    material.SetFloat("_Glossiness", .12f); EditorUtility.SetDirty(material); materials[name] = material;
                }
            }
            AssetDatabase.SaveAssets();
            var parent = new GameObject(id);
            try
            {
                var mesh = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(objPath));
                mesh.transform.SetParent(parent.transform, false);
                var renderers = mesh.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) throw new Exception("Missing mesh: " + id);
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m != null && materials.TryGetValue(m.name, out var mapped) ? mapped : materials.Count == 1 ? materials.Values.First() : throw new Exception("Unmapped material in " + id + ": " + m?.name)).ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                float scale = .20f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                mesh.transform.localScale *= scale;
                mesh.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(mesh.transform);
                return PrefabUtility.SaveAsPrefabAsset(parent, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }

        static void AttachToExistingScene(MuseumCollection collection)
        {
            var scene = SceneManager.GetSceneByPath(PrototypeSetup.ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(PrototypeSetup.ScenePath, OpenSceneMode.Additive);
            var app = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MuseumApp>(true)).Single();
            var ar = app.ar;
            app.collection = collection; ar.collection = collection;
            app.player = app.GetComponent<MuseumAudio>();
            if (app.player == null) app.player = app.gameObject.AddComponent<MuseumAudio>();
            app.player.GetComponent<AudioSource>().playOnAwake = false;
            ar.fallbackListener = ar.fallbackCamera.GetComponent<AudioListener>();
            if (ar.fallbackListener == null) ar.fallbackListener = ar.fallbackCamera.gameObject.AddComponent<AudioListener>();
            var camera = ar.cameraRig.GetComponentsInChildren<Camera>(true).Single();
            ar.arListener = camera.GetComponent<AudioListener>();
            if (ar.arListener == null) ar.arListener = camera.gameObject.AddComponent<AudioListener>();
            ar.arListener.enabled = false; ar.fallbackListener.enabled = true;
            EditorUtility.SetDirty(app); EditorUtility.SetDirty(ar);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
        public static string Verify()
        {
            int expected = ((JArray)JObject.Parse(File.ReadAllText(CatalogPath))["artworks"]).Count;
            var collection = AssetDatabase.LoadAssetAtPath<MuseumCollection>(CollectionPath);
            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
            if (collection.artworks.Count != expected || library.count != expected) throw new Exception("Collection/library mismatch.");
            foreach (var a in collection.artworks)
            {
                if (a.modelPrefab == null || a.photograph == null || a.audio.Count != 6 || a.audio.Any(t => t.clip == null || string.IsNullOrWhiteSpace(t.transcript))) throw new Exception("Incomplete imported content: " + a.id);
                if (!library.Any(i => i.name == a.id)) throw new Exception("Missing reference image: " + a.id);
                if (a.modelPrefab.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterials.Any(m => m == null || m.mainTexture == null))) throw new Exception("Missing material: " + a.id);
            }
            string result = $"Imported {expected} artworks, {library.count} image targets and {collection.artworks.Sum(a => a.audio.Count)} audio tracks. Existing scene preserved; no Play Mode or device test.";
            Debug.Log("[HeritageXR] " + result); return result;
        }
        public static void BuildIOS()
        {
            Sync();
            ExportIOS();
        }
        // Export the already imported collection for code/UI-only changes.
        // BuildIOS remains the entry point whenever catalogue/media data changes.
        public static void ExportIOS()
        {
            string output = Environment.GetEnvironmentVariable("HERITAGEXR_IOS_OUTPUT") ?? "Builds/iOS-S2";
            PlayerSettings.iOS.buildNumber = "6";
            BrandingBuildPreparation.Configure();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { PrototypeSetup.ScenePath }, locationPathName = output, target = BuildTarget.iOS, options = BuildOptions.None });
            var collection = AssetDatabase.LoadAssetAtPath<MuseumCollection>(CollectionPath);
            var summary = new JObject { ["result"] = report.summary.result.ToString(), ["errors"] = report.summary.totalErrors, ["warnings"] = report.summary.totalWarnings, ["outputPath"] = Path.GetFullPath(output), ["artworkCount"] = collection.artworks.Count, ["audioCount"] = collection.artworks.Sum(a => a.audio.Count), ["subtitleTrackCount"] = collection.artworks.Sum(a => a.audio.Count(t => t.subtitles.Count > 0)), ["subtitleCueCount"] = collection.artworks.Sum(a => a.audio.Sum(t => t.subtitles.Count)), ["functionalTestsExecuted"] = false };
            summary["quizCount"] = collection.artworks.Count(a => a.quiz != null);
            summary["quizQuestionCount"] = collection.artworks.Sum(a => a.quiz?.questions.Count ?? 0);
            File.WriteAllText(Environment.GetEnvironmentVariable("HERITAGEXR_BUILD_REPORT") ?? "S2_BUILD_REPORT.json", summary.ToString() + "\n");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("iOS export failed: " + report.summary.result);
        }
    }
}
