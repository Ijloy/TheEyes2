using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkFantasy.EditorTools
{
    /// <summary>
    /// Menú Tools > Caballero Maldito:
    ///  1. Crear animaciones: configura los PNG para pixel art y genera los clips + Animator Controller.
    ///  2. Montar escena de prueba: suelo, caballero, muñeco, cámara pixel perfect.
    ///
    /// Para reemplazar una animación provisional, pon una carpeta nueva en
    /// Assets/Game/Art/CaballeroMaldito con el nombre indicado (ej. "Attack_1") y vuelve a ejecutar el paso 1.
    /// </summary>
    public static class KnightPrototypeSetup
    {
        // Rutas de carpetas y archivos del proyecto que usa este montaje automático.
        const string ArtRoot = "Assets/Game/Art/CaballeroMaldito";
        const string PlaceholderRoot = "Assets/Game/Art/Placeholder";
        const string AnimRoot = "Assets/Game/Animations/CaballeroMaldito";
        const string ControllerPath = AnimRoot + "/CaballeroMaldito.controller";
        const string WeaponArtRoot = "Assets/Game/Art/Armas";
        const string WeaponArtPath = WeaponArtRoot + "/espada_maldita.png";
        const string WeaponDataRoot = "Assets/Game/Data/Armas";
        const string WeaponDataPath = WeaponDataRoot + "/EspadaMaldita.asset";
        // La espada (29x66) se importa a 44 px por unidad para que mida ~1,5 unidades
        const int WeaponPixelsPerUnit = 44;
        static readonly Vector2 GripPivot = new Vector2(0.5f, 10.5f / 66f);
        const string SorcererArtRoot = "Assets/Game/Art/Enemigos/Hechicero";
        const string BackgroundRoot = "Assets/Game/Art/Fondos/CiudadGotica";
        const string ItemArtRoot = "Assets/Game/Art/Objetos";
        const string RuneArtPath = ItemArtRoot + "/runa_oscura.png";
        const string ItemDataRoot = "Assets/Game/Data/Objetos";
        const string RuneDataPath = ItemDataRoot + "/RunaOscuraComun.asset";

        // Píxeles por unidad de todo el arte (32 px = 1 unidad de Unity).
        const int PixelsPerUnit = 32;
        // Los cuadros de PixelLab miden 88x88 y los pies están en la fila 76 desde arriba.
        static readonly Vector2 FeetPivot = new Vector2(0.5f, 12f / 88f);

        // Origen de una animación: carpeta y, opcionalmente, qué cuadros usar.
        struct Source
        {
            public string folder;
            public int[] frames; // null = todos los cuadros
            public Source(string folder, params int[] frames)
            {
                this.folder = folder;
                this.frames = frames != null && frames.Length > 0 ? frames : null;
            }
        }

        // Definición de una animación: nombre, velocidad, si se repite y carpetas posibles.
        class ClipDef
        {
            public string name;
            public float fps;
            public bool loop;
            public Source[] sources; // se usa la primera carpeta que exista
            public ClipDef(string name, float fps, bool loop, params Source[] sources)
            {
                this.name = name; this.fps = fps; this.loop = loop; this.sources = sources;
            }
        }

        // Primero la animación definitiva (si existe la carpeta); después el sustituto provisional.
        static readonly ClipDef[] Clips =
        {
            new ClipDef("Idle", 6, true,
                new Source("Fight_Stance_Idle"), new Source("Breathing_Idle")),
            new ClipDef("Walk", 10, true,
                new Source("Walking"), new Source("Sad_Walk")),
            new ClipDef("JumpUp", 12, false, new Source("Jumping", 3, 4)),
            new ClipDef("JumpFall", 8, true, new Source("Jumping", 5)),
            new ClipDef("Land", 14, false, new Source("Jumping", 7, 8)),
            new ClipDef("Hurt", 12, false,
                new Source("Hurt"), new Source("Reactions"), new Source("Jumping", 2, 1)),
            new ClipDef("Attack1", 14, false,
                new Source("Attack_1"), new Source("Cross_Punch", 2, 3, 4, 5)),
            new ClipDef("Attack2", 14, false,
                new Source("Attack_2"), new Source("Cross_Punch", 2, 3, 4, 5)),
            new ClipDef("Attack3", 14, false,
                new Source("Attack_3"), new Source("Cross_Punch", 2, 3, 4, 5)),
            new ClipDef("Attack4", 10, false,
                new Source("Attack_4"), new Source("Cross_Punch")),
            new ClipDef("Charged", 7, false,
                new Source("Charged_Attack"), new Source("Cross_Punch")),
            new ClipDef("PickUp", 10, false, new Source("Picking_Up")),
        };

        // ------------------------------------------------------------------
        [MenuItem("Tools/Caballero Maldito/1. Crear animaciones")]
        // MENÚ 1 (prototipo lateral): prepara los PNG del caballero y crea las animaciones y el Animator.
        public static void BuildAnimations()
        {
            if (!AssetDatabase.IsValidFolder(ArtRoot))
            {
                EditorUtility.DisplayDialog("Caballero Maldito", "No encuentro la carpeta " + ArtRoot, "OK");
                return;
            }

            // Importa todas las imágenes como pixel art con el pivote en los pies.
            ConfigureTextures(ArtRoot, PixelsPerUnit, FeetPivot);
            EnsureFolder(AnimRoot);

            var created = new List<AnimationClip>();
            var report = new List<string>();

            // Para cada animación usa la primera carpeta que exista (la definitiva o la provisional).
            foreach (var def in Clips)
            {
                List<Sprite> sprites = null;
                string used = null;
                foreach (var src in def.sources)
                {
                    sprites = LoadFrames(src);
                    if (sprites != null && sprites.Count > 0) { used = src.folder; break; }
                }

                if (sprites == null || sprites.Count == 0)
                {
                    report.Add($"✗ {def.name}: no se encontraron cuadros");
                    continue;
                }

                created.Add(SaveClip(def, sprites));
                bool placeholder = used != def.sources[0].folder;
                report.Add($"{(placeholder ? "~" : "✓")} {def.name} ← {used} ({sprites.Count} cuadros){(placeholder ? "  [provisional]" : "")}");
            }

            // Crea el Animator con todas las animaciones, guarda y muestra el resumen.
            BuildController(created);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Caballero Maldito] Animaciones creadas:\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Caballero Maldito",
                "Animaciones creadas.\n\n" + string.Join("\n", report) +
                "\n\n~ = provisional (se reemplaza al añadir la carpeta definitiva).", "OK");
        }

        // ------------------------------------------------------------------
        [MenuItem("Tools/Caballero Maldito/2. Montar escena de prueba")]
        // MENÚ 2 (prototipo lateral): monta la escena de prueba con suelo, caballero, muñeco, hechicero, cámara y fondo.
        public static void BuildTestScene()
        {
            // Si todavía no existen las animaciones, las crea primero.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                BuildAnimations();
                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                if (controller == null) return;
            }

            Sprite white = GetWhiteSprite();
            Sprite firstIdle = controller.animationClips
                .Where(c => c.name == "Idle")
                .Select(FirstSprite)
                .FirstOrDefault();

            // Borra lo que hubiera de un montaje anterior.
            DestroyIfExists("Suelo");
            DestroyIfExists("CaballeroMaldito");
            DestroyIfExists("Muneco");

            // Suelo: la parte de arriba queda en y = -1
            var ground = new GameObject("Suelo");
            ground.transform.position = new Vector3(0f, -4f, 0f);
            ground.transform.localScale = new Vector3(200f, 6f, 1f);
            var gsr = ground.AddComponent<SpriteRenderer>();
            gsr.sprite = white;
            gsr.color = new Color(0.09f, 0.07f, 0.1f);
            ground.AddComponent<BoxCollider2D>();

            // Caballero (pivote en los pies)
            var player = new GameObject("CaballeroMaldito");
            player.transform.position = new Vector3(0f, -0.95f, 0f);
            var psr = player.AddComponent<SpriteRenderer>();
            psr.sprite = firstIdle;
            psr.sortingOrder = 10;
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var box = player.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.75f, 1.75f);
            box.offset = new Vector2(0f, 0.875f);
            var animator = player.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            player.AddComponent<DarkFantasy.PlayerController>();
            player.AddComponent<DarkFantasy.PlayerInventory>();
            SetupWeapon(player);

            // Muñeco de entrenamiento (trigger para poder atravesarlo)
            var dummy = new GameObject("Muneco");
            dummy.transform.position = new Vector3(3.5f, -0.1f, 0f);
            dummy.transform.localScale = new Vector3(0.8f, 1.8f, 1f);
            var dsr = dummy.AddComponent<SpriteRenderer>();
            dsr.sprite = white;
            dsr.color = new Color(0.55f, 0.38f, 0.22f);
            dsr.sortingOrder = 5;
            var dcol = dummy.AddComponent<BoxCollider2D>();
            dcol.isTrigger = true;
            dummy.AddComponent<DarkFantasy.TrainingDummy>();
            SetupLoot(dummy);

            // Hechicero, cámara, luz y fondo.
            CreateSorcerer(new Vector3(9f, -0.95f, 0f));

            SetupCamera(player.transform);
            EnsureGlobalLight();
            CreateBackground();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = player;
            EditorUtility.DisplayDialog("Caballero Maldito",
                "Escena lista: caballero con espada, muñeco y hechicero enemigo. Pulsa Play.\n\nA/D mover · Espacio saltar · J combo · K cargado · H simular golpe.\n\n" +
                "Guarda la escena con Ctrl+S.", "OK");
        }

        // ------------------------------------------------------------------
        [MenuItem("Tools/Caballero Maldito/3. Equipar espada")]
        // MENÚ 3 (prototipo lateral): equipa la espada al caballero que ya está en la escena.
        public static void EquipSwordMenu()
        {
            var player = GameObject.Find("CaballeroMaldito");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Caballero Maldito", "No hay ningún CaballeroMaldito en la escena. Ejecuta antes el paso 2.", "OK");
                return;
            }
            if (SetupWeapon(player))
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("Caballero Maldito", "Espada equipada. Guarda la escena (Ctrl+S) y pulsa Play.", "OK");
            }
        }

        // Crea los hijos 'Arma' y 'Tajo' del caballero y les conecta WeaponVisual.
        static bool SetupWeapon(GameObject player)
        {
            // Datos del arma (se crean si no existen).
            var def = GetOrCreateSwordData();
            if (def == null) return false;

            var pc = player.GetComponent<DarkFantasy.PlayerController>();
            if (pc != null)
            {
                pc.weapon = def;
                EditorUtility.SetDirty(pc);
            }

            DestroyChild(player.transform, "Arma");
            DestroyChild(player.transform, "Tajo");

            var arma = new GameObject("Arma");
            arma.transform.SetParent(player.transform, false);
            var asr = arma.AddComponent<SpriteRenderer>();
            asr.sprite = def.sprite;
            asr.sortingOrder = 11;
            asr.enabled = def.visibleWhenIdle;

            var tajo = new GameObject("Tajo");
            tajo.transform.SetParent(player.transform, false);
            var tsr = tajo.AddComponent<SpriteRenderer>();
            tsr.sortingOrder = 12;
            tsr.enabled = false;

            var wv = player.GetComponent<DarkFantasy.WeaponVisual>();
            if (wv == null) wv = player.AddComponent<DarkFantasy.WeaponVisual>();
            wv.player = pc;
            wv.weaponRenderer = asr;
            wv.slashRenderer = tsr;
            EditorUtility.SetDirty(wv);
            return true;
        }

        [MenuItem("Tools/Caballero Maldito/4. Añadir botín al muñeco")]
        // MENÚ 4: añade al muñeco una tabla de botín (suelta una Runa oscura).
        public static void AddLootMenu()
        {
            var dummy = GameObject.Find("Muneco");
            if (dummy == null)
            {
                EditorUtility.DisplayDialog("Caballero Maldito", "No hay ningún Muneco en la escena. Ejecuta antes el paso 2.", "OK");
                return;
            }
            if (SetupLoot(dummy))
            {
                var player = GameObject.Find("CaballeroMaldito");
                if (player != null && player.GetComponent<DarkFantasy.PlayerInventory>() == null)
                    player.AddComponent<DarkFantasy.PlayerInventory>();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("Caballero Maldito",
                    "El muñeco ahora muere, suelta una Runa oscura y reaparece.\nAcércate a la runa y pulsa E para recogerla.\n\nGuarda la escena (Ctrl+S).", "OK");
            }
        }

        // Configura el LootDropper: suelta siempre 1 Runa oscura.
        static bool SetupLoot(GameObject dummy)
        {
            var rune = GetOrCreateRuneData();
            if (rune == null) return false;

            var dropper = dummy.GetComponent<DarkFantasy.LootDropper>();
            if (dropper == null) dropper = dummy.AddComponent<DarkFantasy.LootDropper>();
            dropper.groundY = -1f;
            dropper.table = new[]
            {
                new DarkFantasy.LootDropper.Entry { item = rune, chance = 1f, min = 1, max = 1 }
            };
            EditorUtility.SetDirty(dropper);
            return true;
        }

        [MenuItem("Tools/Caballero Maldito/5. Añadir hechicero enemigo")]
        // MENÚ 5 (prototipo lateral): añade un hechicero a la derecha del caballero.
        public static void AddSorcererMenu()
        {
            var player = GameObject.Find("CaballeroMaldito");
            float x = player != null ? player.transform.position.x + 9f : 9f;
            if (CreateSorcerer(new Vector3(x, -0.95f, 0f)) != null)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("Caballero Maldito",
                    "Hechicero añadido. Se acerca, lanza bolas de fuego azul, y al morir suelta una Runa oscura.\n\nGuarda la escena (Ctrl+S).", "OK");
            }
        }

        // Crea el hechicero: carga sus animaciones, física, colisión, IA y botín.
        static GameObject CreateSorcerer(Vector3 position)
        {
            if (!AssetDatabase.IsValidFolder(SorcererArtRoot))
            {
                EditorUtility.DisplayDialog("Caballero Maldito", "No encuentro " + SorcererArtRoot, "OK");
                return null;
            }

            // Cuadros de 70x68 con los pies en la última fila
            ConfigureTextures(SorcererArtRoot, PixelsPerUnit, new Vector2(0.5f, 1f / 68f));

            // Carga los cuadros de cada animación (caminar es obligatoria; las demás usan sustitutos si faltan).
            Sprite[] walk = LoadSpritesIn(SorcererArtRoot + "/Walk");
            if (walk.Length == 0)
            {
                EditorUtility.DisplayDialog("Caballero Maldito", "No hay cuadros en " + SorcererArtRoot + "/Walk", "OK");
                return null;
            }
            Sprite[] idle = LoadSpritesIn(SorcererArtRoot + "/Idle");
            Sprite[] attack = LoadSpritesIn(SorcererArtRoot + "/Attack");
            Sprite[] death = LoadSpritesIn(SorcererArtRoot + "/Death");

            DestroyIfExists("Hechicero");
            var go = new GameObject("Hechicero");
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = walk[0];
            sr.sortingOrder = 9;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.9f, 1.9f);
            box.offset = new Vector2(0f, 0.95f);

            // Sus 4 animaciones para el SpriteFrameAnimator.
            var animator = go.AddComponent<DarkFantasy.SpriteFrameAnimator>();
            animator.clips = new[]
            {
                new DarkFantasy.SpriteFrameAnimator.Clip { name = "Idle", frames = idle.Length > 0 ? idle : walk, fps = idle.Length > 0 ? 6f : 3f, loop = true },
                new DarkFantasy.SpriteFrameAnimator.Clip { name = "Walk", frames = walk, fps = 6f, loop = true },
                new DarkFantasy.SpriteFrameAnimator.Clip { name = "Attack", frames = attack.Length > 0 ? attack : new[] { walk[0] }, fps = 7f, loop = false },
                new DarkFantasy.SpriteFrameAnimator.Clip { name = "Death", frames = death.Length > 0 ? death : new[] { walk[0] }, fps = 7f, loop = false },
            };

            go.AddComponent<DarkFantasy.SorcererEnemy>();
            SetupLoot(go);
            return go;
        }

        // Carga todos los PNG de una carpeta como sprites, ordenados por nombre.
        static Sprite[] LoadSpritesIn(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new Sprite[0];
            return Directory.GetFiles(folder, "*.png")
                .Select(f => f.Replace('\\', '/'))
                .OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => AssetDatabase.LoadAssetAtPath<Sprite>(f))
                .Where(sp => sp != null)
                .ToArray();
        }

        [MenuItem("Tools/Caballero Maldito/6. Añadir fondo (ciudad gótica)")]
        // MENÚ 6 (prototipo lateral): añade el fondo de la ciudad gótica con parallax.
        public static void AddBackgroundMenu()
        {
            if (CreateBackground() != null)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("Caballero Maldito",
                    "Fondo con parallax añadido (cielo, ciudad lejana y edificios). Pulsa Play y camina para ver la profundidad.\n\nGuarda la escena (Ctrl+S).", "OK");
            }
        }

        // Crea las 3 capas del fondo (cielo, ciudad lejana y edificios), cada una a su velocidad.
        static GameObject CreateBackground()
        {
            if (!AssetDatabase.IsValidFolder(BackgroundRoot)) return null;
            ConfigureTextures(BackgroundRoot, PixelsPerUnit, new Vector2(0.5f, 0.5f));

            DestroyIfExists("Fondo");
            var root = new GameObject("Fondo");

            // (archivo, cuánto sigue a la cámara, orden de dibujo)
            var layers = new (string file, float follow, int order)[]
            {
                ("fondo_1_cielo.png", 0.95f, -100),
                ("fondo_2_ciudad_lejana.png", 0.8f, -90),
                ("fondo_3_edificios.png", 0.6f, -80),
            };

            foreach (var l in layers)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundRoot + "/" + l.file);
                if (sprite == null) continue;
                var go = new GameObject(Path.GetFileNameWithoutExtension(l.file));
                go.transform.SetParent(root.transform, false);
                var layer = go.AddComponent<DarkFantasy.ParallaxLayer>();
                layer.sprite = sprite;
                layer.followFactor = l.follow;
                layer.sortingOrder = l.order;
            }
            return root;
        }

        // Crea (o actualiza) el archivo de datos de la Runa oscura con su imagen.
        static DarkFantasy.ItemDefinition GetOrCreateRuneData()
        {
            if (AssetImporter.GetAtPath(RuneArtPath) == null)
            {
                EditorUtility.DisplayDialog("Caballero Maldito", "No encuentro " + RuneArtPath, "OK");
                return null;
            }

            ConfigureTextures(ItemArtRoot, PixelsPerUnit, new Vector2(0.5f, 0f));
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RuneArtPath);

            EnsureFolder(ItemDataRoot);
            var def = AssetDatabase.LoadAssetAtPath<DarkFantasy.ItemDefinition>(RuneDataPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<DarkFantasy.ItemDefinition>();
                def.displayName = "Runa oscura";
                def.rarity = DarkFantasy.ItemRarity.Comun;
                def.glowColor = new Color(0.7f, 0.3f, 1f, 1f);
                def.description = "Fragmento del poder de El Que Observa (provisional).";
                def.icon = sprite;
                AssetDatabase.CreateAsset(def, RuneDataPath);
            }
            else
            {
                def.icon = sprite;
                EditorUtility.SetDirty(def);
            }
            AssetDatabase.SaveAssets();
            return def;
        }

        // Crea (o actualiza) el archivo de datos de la espada maldita con su imagen.
        static DarkFantasy.WeaponDefinition GetOrCreateSwordData()
        {
            if (AssetImporter.GetAtPath(WeaponArtPath) == null)
            {
                EditorUtility.DisplayDialog("Caballero Maldito", "No encuentro " + WeaponArtPath, "OK");
                return null;
            }

            ConfigureTextures(WeaponArtRoot, WeaponPixelsPerUnit, GripPivot);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WeaponArtPath);

            EnsureFolder(WeaponDataRoot);
            var def = AssetDatabase.LoadAssetAtPath<DarkFantasy.WeaponDefinition>(WeaponDataPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<DarkFantasy.WeaponDefinition>();
                def.displayName = "Espada maldita (provisional)";
                def.slashColor = new Color(0.78f, 0.35f, 1f, 0.9f);
                def.sprite = sprite;
                AssetDatabase.CreateAsset(def, WeaponDataPath);
            }
            else
            {
                def.sprite = sprite;
                EditorUtility.SetDirty(def);
            }
            AssetDatabase.SaveAssets();
            return def;
        }

        // Borra un hijo por nombre, si existe.
        static void DestroyChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        // ------------------------------------------------------------------
        // Texturas y clips

        // Configura como pixel art todas las imágenes de una carpeta (y sus subcarpetas).
        static void ConfigureTextures(string root, int ppu, Vector2 pivot)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
                ConfigureTexture(AssetDatabase.GUIDToAssetPath(guid), ppu, pivot);
        }

        // Configura una imagen como sprite de pixel art: sin filtro, sin compresión, sin mipmaps y con el pivote dado.
        static void ConfigureTexture(string path, int ppu, Vector2 pivot)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = ppu;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(settings);

            imp.SaveAndReimport();
        }

        // Carga los cuadros de una carpeta del caballero lateral (todos o solo los indicados).
        static List<Sprite> LoadFrames(Source src)
        {
            string dir = ArtRoot + "/" + src.folder;
            if (!AssetDatabase.IsValidFolder(dir)) return null;

            var files = Directory.GetFiles(dir, "*.png")
                .Select(f => f.Replace('\\', '/'))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            if (files.Count == 0) return null;

            IEnumerable<int> indices = src.frames ?? Enumerable.Range(0, files.Count);
            var sprites = new List<Sprite>();
            foreach (int i in indices)
            {
                if (i < 0 || i >= files.Count) continue;
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(files[i]);
                if (sp != null) sprites.Add(sp);
            }
            return sprites;
        }

        // Crea o actualiza un archivo de animación (.anim) que cambia el sprite en cada cuadro.
        static AnimationClip SaveClip(ClipDef def, List<Sprite> sprites)
        {
            var clip = new AnimationClip { name = def.name, frameRate = def.fps };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

            var keys = new ObjectReferenceKeyframe[sprites.Count + 1];
            for (int i = 0; i < sprites.Count; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / def.fps, value = sprites[i] };
            // Clave final para que el último cuadro dure lo mismo que los demás
            keys[sprites.Count] = new ObjectReferenceKeyframe { time = sprites.Count / def.fps, value = sprites[sprites.Count - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            // Si la animación se repite o no.
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = def.loop;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            string path = $"{AnimRoot}/{def.name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                // Conserva el mismo asset (y sus referencias) al regenerar
                EditorUtility.CopySerialized(clip, existing);
                existing.name = def.name;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // Crea o actualiza el Animator del caballero lateral con un estado por animación.
        static void BuildController(List<AnimationClip> clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            var sm = controller.layers[0].stateMachine;
            foreach (var child in sm.states.ToArray())
                sm.RemoveState(child.state);

            // El código cambia de animación con Animator.Play, así que no hacen falta transiciones.
            int row = 0;
            foreach (var clip in clips)
            {
                var st = sm.AddState(clip.name, new Vector3(300f, row * 60f, 0f));
                st.motion = clip;
                if (clip.name == "Idle") sm.defaultState = st;
                row++;
            }
            EditorUtility.SetDirty(controller);
        }

        // Primera imagen de una animación (para mostrarla en el editor).
        static Sprite FirstSprite(AnimationClip clip)
        {
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
                if (keys.Length > 0) return keys[0].value as Sprite;
            }
            return null;
        }

        // ------------------------------------------------------------------
        // Escena

        // Crea (si no existe) un cuadrado blanco de 4x4 para suelo y muñeco provisionales.
        static Sprite GetWhiteSprite()
        {
            EnsureFolder(PlaceholderRoot);
            string path = PlaceholderRoot + "/blanco.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(4, 4);
                var px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            ConfigureTextures(PlaceholderRoot, 4, new Vector2(0.5f, 0.5f));
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Cámara del prototipo lateral: ortográfica, sigue al jugador y es pixel perfect.
        static void SetupCamera(Transform target)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }

            cam.orthographic = true;
            cam.orthographicSize = 360f / PixelsPerUnit / 2f;
            cam.backgroundColor = new Color(0.07f, 0.06f, 0.09f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(target.position.x, target.position.y + 1.5f, -10f);

            var follow = cam.GetComponent<DarkFantasy.CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<DarkFantasy.CameraFollow>();
            follow.target = target;

            AddPixelPerfect(cam);
        }

        // Añade y configura la Pixel Perfect Camera de URP (32 px por unidad, resolución de referencia).
        static void AddPixelPerfect(Camera cam, int refWidth = 640, int refHeight = 360, bool upscaleRT = false)
        {
            // Pixel Perfect Camera de URP (si el paquete está disponible)
            var ppcType = FindType("UnityEngine.Rendering.Universal.PixelPerfectCamera");
            if (ppcType != null)
            {
                var ppc = cam.GetComponent(ppcType);
                if (ppc == null) ppc = cam.gameObject.AddComponent(ppcType);
                var so = new SerializedObject(ppc);
                SetInt(so, "m_AssetsPPU", PixelsPerUnit);
                SetInt(so, "m_RefResolutionX", refWidth);
                SetInt(so, "m_RefResolutionY", refHeight);
                // Renderizar a baja resolución y escalar por un entero: los píxeles miden siempre lo mismo
                // aunque la ventana no sea múltiplo exacto de la resolución de referencia.
                var up = so.FindProperty("m_UpscaleRT");
                if (up != null) up.boolValue = upscaleRT;
                var snap = so.FindProperty("m_PixelSnapping");
                if (snap != null) snap.boolValue = upscaleRT;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Añade una luz global 2D si no hay ninguna (sin luz, URP 2D se ve negro).
        static void EnsureGlobalLight()
        {
            var lightType = FindType("UnityEngine.Rendering.Universal.Light2D");
            if (lightType == null) return;
            if (UnityEngine.Object.FindAnyObjectByType(lightType) != null) return;

            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent(lightType);
            var so = new SerializedObject(light);
            SetInt(so, "m_LightType", 4); // 4 = Global
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        // Utilidades

        // Borra un objeto de la escena por nombre, si existe.
        static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        // Cambia un número de un componente por su nombre interno.
        static void SetInt(SerializedObject so, string prop, int value)
        {
            var p = so.FindProperty(prop);
            if (p == null) return;
            p.intValue = value; // en enums asigna el valor numérico, no el índice
        }

        // Busca un tipo por su nombre completo en todos los ensamblados (por si un paquete no está instalado).
        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        // Crea una carpeta del proyecto (y las de encima) si no existe.
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
