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

        const int PixelsPerUnit = 32;
        // Los cuadros de PixelLab miden 88x88 y los pies están en la fila 76 desde arriba.
        static readonly Vector2 FeetPivot = new Vector2(0.5f, 12f / 88f);

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
        public static void BuildAnimations()
        {
            if (!AssetDatabase.IsValidFolder(ArtRoot))
            {
                EditorUtility.DisplayDialog("Caballero Maldito", "No encuentro la carpeta " + ArtRoot, "OK");
                return;
            }

            ConfigureTextures(ArtRoot, PixelsPerUnit, FeetPivot);
            EnsureFolder(AnimRoot);

            var created = new List<AnimationClip>();
            var report = new List<string>();

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
        public static void BuildTestScene()
        {
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

        static bool SetupWeapon(GameObject player)
        {
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

        static GameObject CreateSorcerer(Vector3 position)
        {
            if (!AssetDatabase.IsValidFolder(SorcererArtRoot))
            {
                EditorUtility.DisplayDialog("Caballero Maldito", "No encuentro " + SorcererArtRoot, "OK");
                return null;
            }

            // Cuadros de 70x68 con los pies en la última fila
            ConfigureTextures(SorcererArtRoot, PixelsPerUnit, new Vector2(0.5f, 1f / 68f));

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
        public static void AddBackgroundMenu()
        {
            if (CreateBackground() != null)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("Caballero Maldito",
                    "Fondo con parallax añadido (cielo, ciudad lejana y edificios). Pulsa Play y camina para ver la profundidad.\n\nGuarda la escena (Ctrl+S).", "OK");
            }
        }

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

        // ------------------------------------------------------------------
        // Vista isométrica (estilo Diablo 2)

        const string IsoRoot = "Assets/Game/Art/Isometrico";
        const string IsoKnightRoot = IsoRoot + "/Caballero";
        const string IsoPlazaPath = IsoRoot + "/Escenarios/plaza_gotica_1280.png";
        const string IsoCutoutRoot = IsoRoot + "/Recortes";
        const string IsoScenePath = "Assets/Scenes/PlazaIsometrica.unity";
        // Cuadros de 57x68 y 68x68 con los pies en la fila 66 desde arriba
        static readonly Vector2 IsoFeetPivot = new Vector2(0.5f, 1f / 68f);
        static readonly Vector2 IsoSpawnPx = new Vector2(800, 470);

        // Borde de la zona caminable, en píxeles de la imagen de la plaza (origen arriba a la izquierda).
        // Al norte va por la base de edificios, verjas, carros y escombros (el caballero siempre queda delante);
        // al sur, por encima de verjas y muros, porque en la imagen plana no pueden tapar al caballero.
        // Lo alto que se puede rodear por detrás (árboles, faroles, pilares) va además en IsoCutouts.
        // Las salidas por las puertas (suroeste, sureste y arco del norte) quedan cerradas.
        static readonly Vector2[] IsoWalkable = Px(
            300,282, 335,256, 385,252, 440,236, 452,212, 500,201, 520,200, 518,214,
            535,232, 560,239, 586,237, 600,223, 640,216, 668,212, 700,213, 722,207,
            728,226, 748,244, 786,244, 796,222, 805,205, 862,205, 868,233, 884,247,
            902,243, 930,262, 966,282, 984,297, 1004,293, 1030,300, 1030,328, 1022,345,
            1022,380, 1030,398, 1048,412, 1066,410, 1062,432, 1066,452, 1070,500, 1040,505,
            1003,512, 1003,598, 968,592, 930,590, 860,594, 800,585, 790,578, 760,603,
            700,618, 660,632, 640,665, 600,680, 573,640, 540,635, 487,612, 470,630,
            447,627, 351,575, 351,553, 312,553, 305,500, 252,500, 250,440, 180,442,
            140,465, 135,405, 170,405, 195,408, 262,422, 300,402, 315,385, 318,370,
            335,358, 340,330, 330,305);

        // Obstáculos dentro de la zona caminable (su base en el suelo), en píxeles de la imagen
        static readonly Vector2[][] IsoObstacles =
        {
            Px(665,388, 703,366, 742,388, 705,410), // base de la estatua
            Px(553,402, 562,385, 591,378, 620,385, 630,402, 618,418, 591,426, 564,418), // pozo
            Px(459,244, 470,238, 481,244, 470,251), // base del farol del noroeste
            Px(810,246, 821,240, 833,246, 821,253), // base del farol del noreste
        };

        // Objetos recortados de la plaza para que el caballero pueda pasar por detrás:
        // (nombre, silueta en píxeles como unión de polígonos, punto de apoyo en el suelo que decide el orden por Y).
        // Los árboles se recortan rama a rama (Stroke) para que los huecos entre ramas no tapen al caballero.
        static readonly (string name, Vector2[][] shapes, Vector2 foot)[] IsoCutouts =
        {
            ("Estatua", Shapes(Px(690,255, 703,255, 712,268, 720,272, 722,300, 718,330, 735,342, 736,390, 705,407, 670,390, 672,345, 680,338, 681,290, 686,268)), new Vector2(703, 388)),
            ("Pozo", Shapes(Px(575,362, 610,362, 624,372, 628,385, 626,410, 610,422, 590,425, 570,423, 558,412, 555,385, 560,370)), new Vector2(591, 400)),
            ("Carro", Shapes(Px(185,415, 200,395, 205,360, 268,335, 308,340, 312,378, 302,402, 268,418, 250,420)), new Vector2(255, 400)),
            ("Cajas", Shapes(Px(1020,352, 1032,328, 1068,326, 1085,343, 1108,341, 1113,370, 1140,380, 1142,400, 1100,402, 1078,406, 1050,405, 1025,395)), new Vector2(1078, 398)),
            // Faroles: poste, brazo y farol
            ("FarolNO", Shapes(Px(465,150, 477,150, 477,237, 480,241, 479,250, 460,250, 459,241, 465,237), Px(470,145, 499,145, 500,158, 470,158),
                Px(478,168, 499,168, 499,196, 478,196), Px(486,157, 491,157, 491,169, 486,169)), new Vector2(470, 249)),
            ("FarolNE", Shapes(Px(818,140, 829,140, 829,238, 833,242, 833,252, 810,252, 810,242, 817,238), Px(788,142, 822,142, 822,160, 788,160),
                Px(791,160, 813,160, 813,198, 791,198)), new Vector2(821, 251)),
            ("FarolE", Shapes(Px(1007,428, 1017,428, 1017,600, 1020,606, 1004,606, 1007,600), Px(978,426, 1017,426, 1017,447, 978,447),
                Px(975,444, 1000,444, 1000,488, 975,488)), new Vector2(1012, 605)),
            ("FarolSO", Shapes(Px(259,428, 272,428, 272,610, 259,610), Px(259,428, 300,428, 300,448, 259,448), Px(279,447, 302,447, 302,490, 279,490)), new Vector2(266, 608)),
            ("FarolSE", Shapes(Px(868,488, 879,488, 879,648, 868,648), Px(868,488, 905,488, 905,510, 868,510), Px(885,505, 910,505, 910,545, 885,545)), new Vector2(873, 646)),
            // Pilares de piedra de las verjas del sur
            ("PilarSO", Shapes(Px(332,514, 342,535, 351,553, 349,560, 348,632, 332,638, 315,632, 314,560, 312,553, 322,535)), new Vector2(332, 636)),
            ("PilarSE", Shapes(Px(949,514, 958,535, 968,553, 966,560, 965,615, 949,622, 932,615, 931,560, 929,553, 939,535)), new Vector2(949, 620)),
            ("PilarE", Shapes(Px(1056,454, 1066,478, 1075,494, 1073,500, 1072,590, 1056,596, 1039,590, 1038,500, 1036,494, 1046,478)), new Vector2(1056, 594)),
            // Árboles muertos del sur: tronco y ramas (grosor, puntos)
            ("ArbolS", Shapes(
                Stroke(12, 460,695, 459,660, 462,620, 464,595, 466,560, 464,540),
                Stroke(7, 464,540, 459,522, 454,505, 452,497),
                Stroke(6, 463,545, 471,527, 474,508),
                Stroke(5, 468,560, 476,545, 479,522),
                Stroke(6, 451,530, 440,520, 428,511),
                Stroke(6, 460,610, 445,598, 430,590, 412,586, 395,588),
                Stroke(5, 443,596, 443,575, 441,548),
                Stroke(5, 425,590, 421,570, 419,550),
                Stroke(5, 412,587, 405,575, 400,562),
                Stroke(6, 466,602, 480,593, 495,588, 510,586),
                Stroke(5, 466,556, 478,546, 490,540, 500,542),
                Stroke(5, 483,592, 490,578, 493,570, 488,566),
                Stroke(5, 464,640, 472,632, 486,624)), new Vector2(460, 692)),
            ("ArbolSE", Shapes(
                Stroke(13, 845,665, 844,640, 843,618, 840,605),
                Stroke(7, 840,608, 828,595, 818,585, 812,565, 810,545, 810,530),
                Stroke(6, 846,612, 852,590, 856,565, 858,545, 854,528),
                Stroke(5, 845,598, 838,575, 832,552),
                Stroke(4, 836,580, 826,574),
                Stroke(6, 828,600, 812,595, 800,592, 796,588, 800,584),
                Stroke(5, 851,590, 862,580, 875,575, 892,570),
                Stroke(5, 852,560, 862,552, 876,546),
                Stroke(5, 848,625, 862,618, 875,612, 888,605),
                Stroke(6, 838,636, 822,638, 800,641),
                Stroke(4, 808,575, 800,555, 797,540)), new Vector2(845, 663)),
        };

        [MenuItem("Tools/Caballero Maldito/8. Montar escena isométrica")]
        public static void BuildIsoScene()
        {
            if (AssetImporter.GetAtPath(IsoPlazaPath) == null)
            {
                EditorUtility.DisplayDialog("Caballero Maldito", "No encuentro " + IsoPlazaPath, "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ConfigureTextures(IsoKnightRoot, PixelsPerUnit, IsoFeetPivot);
            ConfigureTexture(IsoPlazaPath, PixelsPerUnit, Vector2.zero);
            ConfigureYSorting();

            var plaza = AssetDatabase.LoadAssetAtPath<Sprite>(IsoPlazaPath);
            float plazaW = plaza.rect.width, plazaH = plaza.rect.height;
            // La plaza tiene el pivote abajo a la izquierda en (0,0): de píxel de la imagen a mundo
            Vector2 ToWorld(Vector2 px) => new Vector2(px.x / PixelsPerUnit, (plazaH - px.y) / PixelsPerUnit);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Suelo de fondo
            var plazaGo = new GameObject("Plaza");
            var psr = plazaGo.AddComponent<SpriteRenderer>();
            psr.sprite = plaza;
            psr.sortingOrder = -100;

            // Colliders invisibles: borde de la zona caminable, obstáculos sueltos y bordes de la imagen
            var walls = new GameObject("Colisiones");
            walls.transform.SetParent(plazaGo.transform, false);
            walls.AddComponent<EdgeCollider2D>().points =
                IsoWalkable.Append(IsoWalkable[0]).Select(ToWorld).ToArray();
            foreach (var poly in IsoObstacles)
                walls.AddComponent<PolygonCollider2D>().points = poly.Select(ToWorld).ToArray();
            float w = plazaW / PixelsPerUnit, h = plazaH / PixelsPerUnit;
            walls.AddComponent<EdgeCollider2D>().points = new[]
                { Vector2.zero, new Vector2(w, 0f), new Vector2(w, h), new Vector2(0f, h), Vector2.zero };

            // Objetos con orden por Y
            foreach (var c in IsoCutouts)
            {
                var sprite = CreateCutout(c.name.ToLowerInvariant(), c.shapes, c.foot);
                if (sprite == null) continue;
                var go = new GameObject(c.name);
                go.transform.position = ToWorld(c.foot);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
            }

            // Caballero (pivote en los pies, sin gravedad). La raíz lleva física y orden por Y;
            // el hijo "Visual" lleva el sprite y es lo único que se anima, para que el orden no tiemble.
            var player = new GameObject("CaballeroMaldito");
            player.transform.position = ToWorld(IsoSpawnPx);
            player.AddComponent<UnityEngine.Rendering.SortingGroup>();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(player.transform, false);
            var psr2 = visual.AddComponent<SpriteRenderer>();
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var feet = player.AddComponent<CircleCollider2D>();
            feet.radius = 0.22f;
            feet.offset = new Vector2(0f, 0.2f);
            var iso = player.AddComponent<DarkFantasy.IsoPlayerController>();
            iso.directionSprites = DarkFantasy.IsoPlayerController.DirectionNames
                .Select(d => AssetDatabase.LoadAssetAtPath<Sprite>($"{IsoKnightRoot}/Direcciones/caballero_{d}.png"))
                .ToArray();
            iso.visual = psr2;
            iso.pixelsPerUnit = PixelsPerUnit;
            psr2.sprite = iso.directionSprites[0];
            ApplyPixelLabArt(iso);
            EquipIsoCombat(iso);
            CreateIsoDummy(ToWorld(IsoDummyPx));

            SetupIsoCamera(player.transform, new Rect(0f, 0f, w, h));
            EnsureGlobalLight();
            SetGameViewFullHD();

            EditorSceneManager.SaveScene(scene, IsoScenePath);
            Selection.activeGameObject = player;
            Debug.Log("[Caballero Maldito] Escena isométrica guardada en " + IsoScenePath +
                      ". Pulsa Play: WASD o flechas para moverte en 8 direcciones.");
        }

        // ------------------------------------------------------------------
        // Arte de PixelLab para la vista isométrica
        // Estructura: Caballero/PixelLab/Reposo/<direccion>.png
        //             Caballero/PixelLab/Caminar/<direccion>/frame_000.png, frame_001.png...
        // Cuadros de 96x96 (lienzo de las animaciones de PixelLab). Los pies no están siempre a la
        // misma altura (en el reposo a ~18 px del borde inferior, al caminar entre 15 y 22 px), así que
        // el pivote de cada cuadro se pone en su fila opaca más baja: el pie apoyado toca siempre el suelo.

        const string IsoPixelLabRoot = IsoKnightRoot + "/PixelLab";
        static readonly Vector2 PixelLabFeetPivot = new Vector2(0.5f, 18f / 96f); // por si no se puede leer la imagen

        [MenuItem("Tools/Caballero Maldito/9. Cargar animaciones isométricas (PixelLab)")]
        public static void LoadIsoAnimationsMenu()
        {
            var iso = UnityEngine.Object.FindAnyObjectByType<DarkFantasy.IsoPlayerController>();
            if (iso == null)
            {
                EditorUtility.DisplayDialog("Caballero Maldito",
                    "Abre la escena PlazaIsometrica (o ejecuta el paso 8) antes de cargar las animaciones.", "OK");
                return;
            }
            if (!ApplyPixelLabArt(iso))
            {
                EditorUtility.DisplayDialog("Caballero Maldito", "No encuentro arte en " + IsoPixelLabRoot, "OK");
                return;
            }
            EditorUtility.SetDirty(iso);
            EditorSceneManager.MarkSceneDirty(iso.gameObject.scene);
            EditorSceneManager.SaveScene(iso.gameObject.scene);
        }

        /// <summary>Asigna al caballero las imágenes de reposo y los cuadros de caminar y atacar de PixelLab, si existen.</summary>
        static bool ApplyPixelLabArt(DarkFantasy.IsoPlayerController iso)
        {
            if (!AssetDatabase.IsValidFolder(IsoPixelLabRoot)) return false;
            string attackRoot = IsoPixelLabRoot + "/Atacar/";
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IsoPixelLabRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Atacar: pivote fijo a 18 px. El pie que da el paso queda más bajo que en reposo (12-16 px);
                // con el pivote "fila más baja" el cuerpo daría un salto hacia arriba al empezar el golpe.
                var pivot = path.StartsWith(attackRoot, StringComparison.Ordinal)
                    ? PixelLabFeetPivot
                    : LowestOpaquePivot(path, PixelLabFeetPivot);
                ConfigureTexture(path, PixelsPerUnit, pivot);
            }

            var names = DarkFantasy.IsoPlayerController.DirectionNames;
            int idleCount = 0;

            // Reposo: si hay imágenes de PixelLab, sustituyen a las anteriores para que todo cuadre
            var idle = names.Select(d => AssetDatabase.LoadAssetAtPath<Sprite>($"{IsoPixelLabRoot}/Reposo/{d}.png")).ToArray();
            if (idle.Any(x => x != null))
            {
                iso.directionSprites = idle; // las vacías (NO, O, SO) se sacan en espejo
                idleCount = idle.Count(x => x != null);
            }

            iso.walkFrames = LoadDirectionFrames("Caminar", out int walkCount);
            iso.attackFrames = LoadDirectionFrames("Atacar", out int attackCount);

            if (iso.visual != null && iso.directionSprites.Length > 0 && iso.directionSprites[0] != null)
            {
                iso.visual.sprite = iso.directionSprites[0];
                iso.visual.transform.localScale = Vector3.one;
                iso.visual.transform.localPosition = Vector3.zero;
            }
            Debug.Log($"[Caballero Maldito] Arte de PixelLab cargado: {idleCount} imágenes de reposo, caminar en {walkCount} direcciones " +
                      $"y atacar en {attackCount} (el resto en espejo; sin ataque se usa la imagen de reposo).");
            return idleCount > 0 || walkCount > 0 || attackCount > 0;
        }

        /// <summary>Cuadros de PixelLab/&lt;animación&gt;/&lt;dirección&gt;/frame_*.png para las 8 direcciones (vacías si no hay carpeta).</summary>
        static DarkFantasy.IsoPlayerController.DirectionFrames[] LoadDirectionFrames(string animation, out int directionsFound)
        {
            var names = DarkFantasy.IsoPlayerController.DirectionNames;
            var result = new DarkFantasy.IsoPlayerController.DirectionFrames[names.Length];
            directionsFound = 0;
            for (int i = 0; i < names.Length; i++)
            {
                result[i] = new DarkFantasy.IsoPlayerController.DirectionFrames();
                string folder = $"{IsoPixelLabRoot}/{animation}/{names[i]}";
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                result[i].frames = AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                    .Where(x => x != null)
                    .ToArray();
                if (result[i].frames.Length > 0) directionsFound++;
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Combate isométrico: combo de 4 tajos y muñeco de entrenamiento
        // Estructura: Efectos/Tajos/<n>_<nombre>/frame_000.png... (uno por golpe, en orden, mirando al este)

        const string IsoSlashRoot = "Assets/Game/Art/Efectos/Tajos";
        const string IsoDummyPath = IsoRoot + "/Muneco/muneco_entrenamiento.png";
        static readonly Vector2 IsoDummyPx = new Vector2(880, 455);

        [MenuItem("Tools/Caballero Maldito/10. Añadir combate isométrico")]
        public static void AddIsoCombatMenu()
        {
            var iso = UnityEngine.Object.FindAnyObjectByType<DarkFantasy.IsoPlayerController>();
            if (iso == null)
            {
                EditorUtility.DisplayDialog("Caballero Maldito",
                    "Abre la escena PlazaIsometrica (o ejecuta el paso 8) antes de añadir el combate.", "OK");
                return;
            }
            EquipIsoCombat(iso);
            var plaza = AssetDatabase.LoadAssetAtPath<Sprite>(IsoPlazaPath);
            CreateIsoDummy(new Vector2(IsoDummyPx.x, plaza.rect.height - IsoDummyPx.y) / PixelsPerUnit);
            EditorUtility.SetDirty(iso);
            EditorSceneManager.MarkSceneDirty(iso.gameObject.scene);
            EditorSceneManager.SaveScene(iso.gameObject.scene);
        }

        /// <summary>Crea el hijo "Tajo" del caballero y le asigna los cuadros de cada golpe del combo.</summary>
        static void EquipIsoCombat(DarkFantasy.IsoPlayerController iso)
        {
            DestroyChild(iso.transform, "Tajo");
            var tajo = new GameObject("Tajo");
            tajo.transform.SetParent(iso.transform, false);
            var sr = tajo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 1;
            sr.enabled = false;
            iso.slashRenderer = sr;

            if (!AssetDatabase.IsValidFolder(IsoSlashRoot))
            {
                Debug.LogWarning("[Caballero Maldito] No encuentro los tajos en " + IsoSlashRoot);
                return;
            }
            ConfigureTextures(IsoSlashRoot, PixelsPerUnit, new Vector2(0.5f, 0.5f));

            // Una carpeta por golpe, ordenadas por nombre (1_..., 2_..., 3_..., 4_...)
            var folders = AssetDatabase.GetSubFolders(IsoSlashRoot).OrderBy(f => f, StringComparer.Ordinal).ToArray();
            if (iso.combo == null || iso.combo.Length == 0) return;
            for (int i = 0; i < iso.combo.Length && i < folders.Length; i++)
            {
                iso.combo[i].slashFrames = AssetDatabase.FindAssets("t:Sprite", new[] { folders[i] })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                    .Where(s => s != null)
                    .ToArray();
            }
            Debug.Log($"[Caballero Maldito] Combo isométrico: {Mathf.Min(iso.combo.Length, folders.Length)} tajos cargados de {IsoSlashRoot}.");
        }

        /// <summary>Muñeco de entrenamiento de la plaza: sólido, ordenado por Y y con la barra de vida encima.</summary>
        static void CreateIsoDummy(Vector2 worldPos)
        {
            DestroyIfExists("Muneco");
            if (AssetImporter.GetAtPath(IsoDummyPath) == null)
            {
                Debug.LogWarning("[Caballero Maldito] No encuentro el sprite del muñeco en " + IsoDummyPath);
                return;
            }
            ConfigureTexture(IsoDummyPath, PixelsPerUnit, LowestOpaquePivot(IsoDummyPath, new Vector2(0.5f, 0f)));

            var dummy = new GameObject("Muneco");
            dummy.transform.position = worldPos;
            var sr = dummy.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IsoDummyPath);
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            // Collider en la base (sólido: el caballero choca con él y los golpes lo encuentran)
            var col = dummy.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;
            col.offset = new Vector2(0f, 0.2f);
            var td = dummy.AddComponent<DarkFantasy.TrainingDummy>();
            td.uiHeight = 1.75f;
            td.flashColor = new Color(1f, 0.55f, 0.55f);
        }

        static void SetupIsoCamera(Transform target, Rect bounds)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = IsoRefHeight / (float)PixelsPerUnit / 2f;
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;

            var follow = go.AddComponent<DarkFantasy.CameraFollow>();
            follow.target = target;
            follow.offset = new Vector2(0f, 1f);
            // Se adelanta hacia donde camina el caballero (más en horizontal: la pantalla es 16:9)
            follow.lookAhead = new Vector2(3f, 1.7f);
            follow.useBounds = true;
            follow.bounds = bounds;
            go.transform.position = follow.ClampToBounds(new Vector3(target.position.x, target.position.y + 1f, -10f));

            AddPixelPerfect(cam, IsoRefWidth, IsoRefHeight, upscaleRT: true);
        }

        // Resolución de referencia de la vista isométrica. En 1080p la Pixel Perfect Camera solo admite
        // zooms enteros: 480x270 (x4, cerca), 640x360 (x3) o 960x540 (x2, lejos). 640x360 deja ver
        // más alrededor (enemigos) sin que el personaje quede diminuto; escala x2 a 720p, x4 a 1440p y x6 a 4K.
        const int IsoRefWidth = 640, IsoRefHeight = 360;

        /// <summary>
        /// Pone la vista Game en una resolución fija de 1920x1080 (16:9, múltiplo exacto de 480x270 y 640x360)
        /// para que la Pixel Perfect Camera no avise de "odd-numbered resolution".
        /// Unity no tiene API pública para esto: se usa reflexión sobre GameViewSizes / GameView.
        /// </summary>
        static void SetGameViewFullHD()
        {
            const int W = 1920, H = 1080;
            const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            try
            {
                var asm = typeof(EditorWindow).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var sizeKind = asm.GetType("UnityEditor.GameViewSizeType");
                var gameViewType = asm.GetType("UnityEditor.GameView");

                var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
                var group = sizesType.GetProperty("currentGroup", Any).GetValue(sizes);
                var groupType = group.GetType();

                int count = (int)groupType.GetMethod("GetTotalCount", Any).Invoke(group, null);
                int index = -1;
                for (int i = 0; i < count && index < 0; i++)
                {
                    var s = groupType.GetMethod("GetGameViewSize", Any).Invoke(group, new object[] { i });
                    if ((int)sizeType.GetProperty("width", Any).GetValue(s) == W &&
                        (int)sizeType.GetProperty("height", Any).GetValue(s) == H &&
                        sizeType.GetProperty("sizeType", Any).GetValue(s).ToString() == "FixedResolution")
                        index = i;
                }
                if (index < 0)
                {
                    var fixedRes = Enum.Parse(sizeKind, "FixedResolution");
                    var size = Activator.CreateInstance(sizeType, fixedRes, W, H, "Pixel Perfect 16:9");
                    groupType.GetMethod("AddCustomSize", Any).Invoke(group, new[] { size });
                    index = count;
                }

                var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
                var select = gameViewType.GetMethod("SizeSelectionCallback", Any);
                if (select != null) select.Invoke(gameView, new object[] { index, null });
                else gameViewType.GetProperty("selectedSizeIndex", Any).SetValue(gameView, index);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Caballero Maldito] No pude poner la vista Game en 1920x1080; ponla a mano. " + e.Message);
            }
        }

        /// <summary>Orden de dibujo por el eje Y: lo que está más abajo en pantalla se dibuja delante.</summary>
        static void ConfigureYSorting()
        {
            UnityEngine.Rendering.GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
            UnityEngine.Rendering.GraphicsSettings.transparencySortAxis = Vector3.up;

            // En URP 2D manda el ajuste del Renderer2D
            var type = FindType("UnityEngine.Rendering.Universal.Renderer2DData");
            if (type == null) return;
            foreach (var guid in AssetDatabase.FindAssets("t:" + type.Name, new[] { "Assets" }))
            {
                var data = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type);
                if (data == null) continue;
                var so = new SerializedObject(data);
                SetInt(so, "m_TransparencySortMode", (int)TransparencySortMode.CustomAxis);
                var axis = so.FindProperty("m_TransparencySortAxis");
                if (axis != null) axis.vector3Value = Vector3.up;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Recorta un objeto de la imagen de la plaza (lo de fuera de la silueta queda transparente)
        /// y lo guarda como sprite con el pivote en su punto de apoyo.
        /// </summary>
        static Sprite CreateCutout(string fileName, Vector2[][] shapes, Vector2 foot)
        {
            var src = new Texture2D(2, 2);
            if (!src.LoadImage(File.ReadAllBytes(IsoPlazaPath))) return null;
            Color32[] srcPx = src.GetPixels32();
            int srcW = src.width, srcH = src.height;
            UnityEngine.Object.DestroyImmediate(src);

            var all = shapes.SelectMany(s => s).ToArray();
            int minX = Mathf.Max(0, Mathf.FloorToInt(all.Min(p => p.x))), maxX = Mathf.Min(srcW, Mathf.CeilToInt(all.Max(p => p.x)));
            int minY = Mathf.Max(0, Mathf.FloorToInt(all.Min(p => p.y))), maxY = Mathf.Min(srcH, Mathf.CeilToInt(all.Max(p => p.y)));
            int w = maxX - minX, h = maxY - minY;

            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int iy = maxY - 1 - y; // fila en la imagen (origen arriba); la textura va de abajo arriba
                for (int x = 0; x < w; x++)
                {
                    int ix = minX + x;
                    var c = new Vector2(ix + 0.5f, iy + 0.5f);
                    bool inside = shapes.Any(s => InsidePolygon(c, s));
                    px[y * w + x] = inside ? srcPx[(srcH - 1 - iy) * srcW + ix] : new Color32(0, 0, 0, 0);
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            EnsureFolder(IsoCutoutRoot);
            string path = $"{IsoCutoutRoot}/{fileName}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            ConfigureTexture(path, PixelsPerUnit, new Vector2((foot.x - minX) / w, (maxY - foot.y) / h));
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static bool InsidePolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        static Vector2[] Px(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return pts;
        }

        /// <summary>Junta polígonos sueltos (Px) y trazos (Stroke) en una sola silueta.</summary>
        static Vector2[][] Shapes(params object[] parts) =>
            parts.SelectMany(p => p is Vector2[][] many ? many : new[] { (Vector2[])p }).ToArray();

        /// <summary>
        /// Línea con grosor (en píxeles) que pasa por los puntos dados, como polígonos:
        /// un rectángulo por tramo y un octógono en cada punto para redondear las uniones.
        /// </summary>
        static Vector2[][] Stroke(float width, params float[] xy)
        {
            var pts = Px(xy);
            float r = width / 2f;
            var polys = new List<Vector2[]>();
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                Vector2 n = Vector2.Perpendicular((b - a).normalized) * r;
                polys.Add(new[] { a + n, b + n, b - n, a - n });
            }
            foreach (var p in pts)
                polys.Add(Enumerable.Range(0, 8)
                    .Select(k => p + new Vector2(Mathf.Cos(k * Mathf.PI / 4f), Mathf.Sin(k * Mathf.PI / 4f)) * r)
                    .ToArray());
            return polys.ToArray();
        }

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

        static void DestroyChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        // ------------------------------------------------------------------
        // Texturas y clips

        static void ConfigureTextures(string root, int ppu, Vector2 pivot)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
                ConfigureTexture(AssetDatabase.GUIDToAssetPath(guid), ppu, pivot);
        }

        /// <summary>Pivote centrado en horizontal y en la fila opaca más baja de la imagen (los pies).</summary>
        static Vector2 LowestOpaquePivot(string path, Vector2 fallback)
        {
            var tex = new Texture2D(2, 2);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(path))) return fallback;
                var px = tex.GetPixels32(); // fila 0 = abajo
                for (int y = 0; y < tex.height; y++)
                    for (int x = 0; x < tex.width; x++)
                        if (px[y * tex.width + x].a > 20)
                            return new Vector2(0.5f, (float)y / tex.height);
                return fallback;
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

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

        static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        static void SetInt(SerializedObject so, string prop, int value)
        {
            var p = so.FindProperty(prop);
            if (p == null) return;
            p.intValue = value; // en enums asigna el valor numérico, no el índice
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
