using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Menu "Projet Arc > Configurer le projet".
/// Pose pour de vrai dans les scènes tout ce qui apparaissait avant par script :
/// cibles et oiseaux en réserve, lignes du panneau, colliders des cibles, EventSystem VR...
/// et ajoute : couleurs des arcs / flèches, traînées, tir triple du Bow_Monkey, carquois dans le dos, chrono de 5 min.
/// Peut être relancé sans risque : il refait ce qu'il a créé.
/// </summary>
public static partial class ProjetArcSetup
{
    const string MatFolder = "Assets/Materials/Archery";
    const string SideLeft = "Flèche gauche (tir triple)";
    const string SideRight = "Flèche droite (tir triple)";
    const string QuiverName = "Carquois";
    const string TrailName = "Traînée";
    const string WristName = "Montre chrono";
    const string BoardChronoName = "Chrono";
    const string TimerName = "Chrono du niveau";
    const string AimName = "Aide à la visée";
    const int ArrowsPerTypeInQuiver = 3;

    static readonly string[] Scenes =
    {
        "Assets/Open_World.unity", "Assets/Niveau1.unity", "Assets/Niveau2.unity", "Assets/Niveau3.unity",
    };

    // ---------- Styles des 4 familles ----------

    class Style
    {
        public Color bow, bowAccent, arrow, arrowAccent, str;
        public Color emission = Color.black;
        public Color[] trail;
        public float trailWidth, trailTime;
        public bool flicker;
        public bool electric;                   // effets électriques (particules, traînée éclair)
        public Color accentEmission = Color.black;
    }

    static readonly Dictionary<string, Style> Styles = new Dictionary<string, Style>
    {
        [ArcheryTypes.Base] = new Style
        {
            bow = new Color(0.45f, 0.27f, 0.12f), bowAccent = new Color(0.95f, 0.75f, 0.25f),
            arrow = new Color(0.85f, 0.68f, 0.42f), arrowAccent = new Color(0.75f, 0.75f, 0.8f),
            str = new Color(0.95f, 0.9f, 0.8f),
            trail = new[] { Color.white, new Color(1f, 0.82f, 0.3f) }, trailWidth = 0.06f, trailTime = 0.5f,
        },
        [ArcheryTypes.Bio] = new Style
        {
            bow = new Color(0.16f, 0.6f, 0.24f), bowAccent = new Color(0.6f, 0.92f, 0.3f),
            arrow = new Color(0.3f, 0.82f, 0.35f), arrowAccent = new Color(0.85f, 1f, 0.4f),
            str = new Color(0.75f, 1f, 0.5f),
            trail = new[] { new Color(0.65f, 1f, 0.3f), new Color(0.1f, 0.7f, 0.55f) }, trailWidth = 0.09f, trailTime = 0.7f,
        },
        [ArcheryTypes.Fulminaleuse] = new Style
        {
            // Violet orage + éclairs jaunes
            bow = new Color(0.3f, 0.07f, 0.55f), bowAccent = new Color(1f, 0.85f, 0.15f),
            arrow = new Color(0.42f, 0.12f, 0.72f), arrowAccent = new Color(1f, 0.9f, 0.3f),
            str = new Color(1f, 0.95f, 0.55f), emission = new Color(0.35f, 0.08f, 0.7f) * 0.8f,
            accentEmission = new Color(1f, 0.8f, 0.1f) * 2.5f,
            trail = new[] { new Color(1f, 1f, 0.75f), new Color(1f, 0.85f, 0.1f), new Color(0.6f, 0.2f, 1f) }, trailWidth = 0.09f, trailTime = 0.4f,
            flicker = true, electric = true,
        },
        [ArcheryTypes.Monkey] = new Style
        {
            bow = new Color(1f, 0.6f, 0.08f), bowAccent = new Color(0.45f, 0.25f, 0.08f),
            arrow = new Color(1f, 0.86f, 0.2f), arrowAccent = new Color(0.45f, 0.25f, 0.08f),
            str = new Color(1f, 0.85f, 0.4f),
            trail = new[] { new Color(1f, 0.95f, 0.3f), new Color(1f, 0.5f, 0.1f), new Color(0.9f, 0.15f, 0.1f) }, trailWidth = 0.11f, trailTime = 0.6f,
        },
    };

    static Gradient MakeGradient(Color[] colors)
    {
        var ck = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i], colors.Length == 1 ? 0f : i / (float)(colors.Length - 1));
        var g = new Gradient();
        g.SetKeys(ck, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.75f, 0.5f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    // ---------- Menu ----------

    [MenuItem("Projet Arc/Configurer le projet (toutes les scènes)")]
    public static void ConfigureAll()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[Projet Arc] Quitte le mode Play d'abord."); return; }
        EditorSceneManager.SaveOpenScenes();
        string back = SceneManager.GetActiveScene().path;

        FixTargetPrefabs();
        foreach (var path in Scenes)
        {
            if (!File.Exists(path)) { Debug.LogWarning($"[Projet Arc] Scène absente : {path}"); continue; }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ConfigureScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Projet Arc] Scène configurée et sauvegardée : {path}");
        }
        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(back) && File.Exists(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
        Debug.Log("[Projet Arc] Configuration terminée.");
    }

    [MenuItem("Projet Arc/Configurer la scène ouverte")]
    public static void ConfigureOpen()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[Projet Arc] Quitte le mode Play d'abord."); return; }
        FixTargetPrefabs();
        var scene = SceneManager.GetActiveScene();
        ConfigureScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Projet Arc] Scène configurée et sauvegardée : {scene.path}");
    }

    static void ConfigureScene(Scene scene)
    {
        RemoveMissingPrefabs(scene);
        FixMisplacedTargets(scene);
        FixNegativeScaleBoxColliders(scene);
        SetupEventSystem(scene);
        SetupBows(scene);
        SetupAimGuides(scene);
        SetupQuiver(scene);
        SetupArrows(scene);     // après le carquois et le tir triple : couvre aussi les flèches copiées
        SetupTargets(scene);
        SetupTargetSpawners(scene);
        KeepSingleBird(scene);
        SetupBirdSpawners(scene);
        SetupResultsBoards(scene);
        SetupTimer(scene);
    }

    // ---------- Outils ----------

    static List<T> All<T>(Scene s) where T : Component
    {
        var list = new List<T>();
        foreach (var root in s.GetRootGameObjects()) list.AddRange(root.GetComponentsInChildren<T>(true));
        return list;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var d = FindDeep(child, name);
            if (d != null) return d;
        }
        return null;
    }

    static void DestroyChild(Transform parent, string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var c = parent.GetChild(i);
            if (c.name != name) continue;
            if (PrefabUtility.IsPartOfPrefabInstance(c.gameObject) && !PrefabUtility.IsAddedGameObjectOverride(c.gameObject)
                && PrefabUtility.GetOutermostPrefabInstanceRoot(c.gameObject) != c.gameObject)
                continue;   // fait partie d'un prefab : on n'y touche pas
            Object.DestroyImmediate(c.gameObject);
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Material LitMaterial(string name, Color color, Color emission, float smoothness = 0.35f)
    {
        EnsureFolder(MatFolder);
        string path = $"{MatFolder}/{name}.mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        else if (mat.shader != shader) mat.shader = shader;

        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (emission.maxColorComponent > 0.001f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material UnlitVertexMaterial(string name)
    {
        EnsureFolder(MatFolder);
        string path = $"{MatFolder}/{name}.mat";
        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.color = Color.white;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void Paint(Renderer r, Material main, Material accent)
    {
        var mats = r.sharedMaterials;
        int n = Mathf.Max(1, mats.Length);
        var result = new Material[n];
        for (int i = 0; i < n; i++) result[i] = i == 0 ? main : accent;
        r.sharedMaterials = result;
        EditorUtility.SetDirty(r);
    }

    // ---------- Prefabs cassés ----------

    /// <summary>Supprime les objets dont le prefab n'existe plus (ex. ancien "PanneauResultat" en double dans Niveau1/2).</summary>
    static void RemoveMissingPrefabs(Scene scene)
    {
        var broken = new List<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (PrefabUtility.GetPrefabInstanceStatus(go) != PrefabInstanceStatus.MissingAsset) continue;
                if (PrefabUtility.GetOutermostPrefabInstanceRoot(go) != go) continue;
                broken.Add(go);
            }
        foreach (var go in broken)
        {
            if (go == null) continue;
            Debug.Log($"[Projet Arc] {scene.name} : objet '{go.name}' supprimé (son prefab n'existe plus).");
            Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// Un "Target" (cible à anneaux) posé sur un objet qui n'a pas d'anneaux "disque…" (ex. l'oiseau "cardinal")
    /// ne peut rien compter : on le remplace par BirdTarget (cible bonus, l'oiseau s'envole quand il est touché).
    /// </summary>
    static void FixMisplacedTargets(Scene scene)
    {
        foreach (var t in All<Target>(scene))
        {
            bool hasRings = false;
            foreach (Transform c in t.transform)
                if (c.name.StartsWith(t.ringPrefix) && c.GetComponent<MeshFilter>() != null) { hasRings = true; break; }
            if (hasRings) continue;

            var go = t.gameObject;
            var bt = GetOrAdd<BirdTarget>(go);
            bt.points = 100;
            bt.reaction = BirdTarget.Reaction.SEnvole;
            EditorUtility.SetDirty(bt);

            if (PrefabUtility.IsPartOfPrefabInstance(t) && !PrefabUtility.IsAddedComponentOverride(t)) t.enabled = false;
            else Object.DestroyImmediate(t);
            Debug.Log($"[Projet Arc] {scene.name} : '{go.name}' n'est pas une cible à anneaux : Target remplacé par BirdTarget (+100).", go);
        }
    }

    /// <summary>Les BoxCollider n'acceptent pas d'échelle négative (avertissements au lancement) : on remet l'échelle en positif.</summary>
    static void FixNegativeScaleBoxColliders(Scene scene)
    {
        foreach (var box in All<BoxCollider>(scene))
        {
            for (var t = box.transform; t != null; t = t.parent)
            {
                var s = t.localScale;
                if (s.x >= 0f && s.y >= 0f && s.z >= 0f) continue;
                t.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                EditorUtility.SetDirty(t);
            }
        }
    }

    // ---------- EventSystem VR ----------

    static void SetupEventSystem(Scene scene)
    {
        var systems = All<EventSystem>(scene);
        EventSystem es;
        if (systems.Count == 0)
        {
            var go = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(go, scene);
            es = go.AddComponent<EventSystem>();
        }
        else es = systems[0];

        GetOrAdd<XRUIInputModule>(es.gameObject).enabled = true;
        foreach (var m in es.GetComponents<BaseInputModule>())
            if (!(m is XRUIInputModule)) { m.enabled = false; EditorUtility.SetDirty(m); }

        var cam = All<XROrigin>(scene).Select(o => o.Camera).FirstOrDefault(c => c != null);
        foreach (var canvas in All<Canvas>(scene))
        {
            if (canvas.renderMode != RenderMode.WorldSpace || !canvas.isRootCanvas) continue;
            if (canvas.worldCamera == null && cam != null) canvas.worldCamera = cam;
            GetOrAdd<TrackedDeviceGraphicRaycaster>(canvas.gameObject);
            GetOrAdd<GraphicRaycaster>(canvas.gameObject);
            EditorUtility.SetDirty(canvas);
        }
    }

    // ---------- Arcs ----------

    static bool IsQuiverOrSide(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            if (p.name == QuiverName || p.name == SideLeft || p.name == SideRight) return true;
        return false;
    }

    static void SetupBows(Scene scene)
    {
        var exemplars = ArrowExemplars(scene);

        foreach (var bow in All<BowController>(scene))
        {
            string type = ArcheryTypes.Of(bow.name);
            var st = Styles[type];
            var main = LitMaterial($"Arc_{type}", st.bow, st.emission);
            var accent = LitMaterial($"Arc_{type}_Detail", st.bowAccent, st.accentEmission.maxColorComponent > 0f ? st.accentEmission : st.emission * 0.6f);

            foreach (var r in bow.GetComponentsInChildren<Renderer>(true))
            {
                if (r is TrailRenderer || r is ParticleSystemRenderer) continue;
                if (r.GetComponentInParent<ArrowController>(true) != null) continue;   // flèches posées sur l'arc
                if (IsUnder(r.transform, ElecName)) continue;                        // effets électriques
                if (IsUnder(r.transform, AimName)) continue;                         // aide à la visée
                if (r is LineRenderer lr)
                {
                    lr.sharedMaterial = UnlitVertexMaterial("Corde");
                    lr.startColor = lr.endColor = st.str;
                    EditorUtility.SetDirty(lr);
                    continue;
                }
                Paint(r, main, accent);
            }

            bow.trailColors = MakeGradient(st.trail);
            bow.trailWidth = st.trailWidth;
            bow.trailTime = st.trailTime;
            bow.trailFlicker = st.flicker;
            bow.trailSparks = st.electric;
            bow.noGravity = type == ArcheryTypes.Fulminaleuse;   // tire tout droit
            bow.trailMaterial = st.electric ? ElectricTrailMaterial() : null;
            SetupBowElectricity(bow, st.electric);
            EditorUtility.SetDirty(bow);

            if (type == ArcheryTypes.Monkey) SetupTripleShot(bow, exemplars);
        }
    }

    static bool IsUnder(Transform t, string name)
    {
        for (var p = t; p != null; p = p.parent) if (p.name == name) return true;
        return false;
    }

    // ---------- Aide à la visée ----------

    static LineRenderer AimLine(Transform parent, string name, Gradient colors, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = UnlitVertexMaterial("Trainee");
        lr.useWorldSpace = true;
        lr.positionCount = 0;
        lr.widthMultiplier = width;
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.6f));
        lr.colorGradient = colors;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
        return lr;
    }

    static void SetupAimGuides(Scene scene)
    {
        foreach (var bow in All<BowController>(scene))
        {
            DestroyChild(bow.transform, AimName);
            var st = Styles[ArcheryTypes.Of(bow.name)];

            var root = new GameObject(AimName).transform;
            root.SetParent(bow.transform, false);

            // Couleur de l'arc, de plus en plus transparente vers la fin de la trajectoire
            Color c = st.trail[0];
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(st.trail[st.trail.Length - 1], 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0.15f, 1f) });
            var gSide = new Gradient();
            gSide.SetKeys(g.colorKeys, new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0.3f, 0.7f), new GradientAlphaKey(0.05f, 1f) });

            var guide = GetOrAdd<AimGuide>(bow.gameObject);
            guide.line = AimLine(root, "Trajectoire", g, 0.015f);
            guide.leftLine = guide.rightLine = null;
            if (bow.GetComponent<TripleShot>() != null)
            {
                guide.leftLine = AimLine(root, "Trajectoire gauche", gSide, 0.01f);
                guide.rightLine = AimLine(root, "Trajectoire droite", gSide, 0.01f);
            }

            // Point d'impact : petit disque plat à la couleur de l'arc, sans collider
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Point d'impact";
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(root, false);
            marker.transform.localScale = new Vector3(0.12f, 0.004f, 0.12f);
            var pivot = new GameObject("Repère").transform;   // son axe Z regarde la surface touchée, le disque est couché dessus
            pivot.SetParent(root, false);
            marker.transform.SetParent(pivot, false);
            marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            marker.GetComponent<Renderer>().sharedMaterial = LitMaterial($"Impact_{ArcheryTypes.Of(bow.name)}", c, c * 1.5f, 0.2f);
            marker.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pivot.gameObject.SetActive(false);
            guide.impactMarker = pivot;

            EditorUtility.SetDirty(guide);
        }
    }

    /// <summary>Une flèche "modèle" de chaque famille, posée dans la scène (pas une copie du carquois / tir triple).</summary>
    static Dictionary<string, ArrowController> ArrowExemplars(Scene scene)
    {
        var d = new Dictionary<string, ArrowController>();
        foreach (var a in All<ArrowController>(scene))
        {
            if (IsQuiverOrSide(a.transform)) continue;
            string t = ArcheryTypes.Of(a.name);
            if (!d.ContainsKey(t)) d[t] = a;
        }
        return d;
    }

    static ArrowController CopyArrow(ArrowController source, string name, Transform parent)
    {
        var go = Object.Instantiate(source.gameObject);
        go.name = name;
        SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
        go.transform.SetParent(parent, true);
        return go.GetComponent<ArrowController>();
    }

    static void AlignArrow(ArrowController a, Vector3 nockPos, Vector3 forward)
    {
        if (a.tip == null || a.grabPoint == null) return;
        Vector3 axis = (a.tip.position - a.grabPoint.position).normalized;
        a.transform.rotation = Quaternion.FromToRotation(axis, forward) * a.transform.rotation;
        a.transform.position += nockPos - a.grabPoint.position;
    }

    static void SetupTripleShot(BowController bow, Dictionary<string, ArrowController> exemplars)
    {
        DestroyChild(bow.transform, SideLeft);
        DestroyChild(bow.transform, SideRight);

        if (!exemplars.TryGetValue(ArcheryTypes.Monkey, out var model)) model = exemplars.Values.FirstOrDefault();
        if (model == null) { Debug.LogWarning($"[Projet Arc] Pas de flèche dans la scène pour le tir triple de {bow.name}."); return; }

        var shot = GetOrAdd<TripleShot>(bow.gameObject);
        Transform nock = bow.arrowNock != null ? bow.arrowNock : bow.transform;

        ArrowHit Make(string name, float side)
        {
            var a = CopyArrow(model, name, bow.transform);
            AlignArrow(a, nock.position + nock.right * side, nock.forward);
            a.gameObject.SetActive(false);
            return GetOrAdd<ArrowHit>(a.gameObject);
        }

        shot.leftArrow = Make(SideLeft, -shot.sideOffset);
        shot.rightArrow = Make(SideRight, shot.sideOffset);
        EditorUtility.SetDirty(shot);
    }

    // ---------- Flèches : couleurs, score, traînée ----------

    static void SetupArrows(Scene scene)
    {
        var trailMat = UnlitVertexMaterial("Trainee");

        foreach (var arrow in All<ArrowController>(scene))
        {
            string type = ArcheryTypes.Of(arrow.name);
            var st = Styles[type];
            var main = LitMaterial($"Fleche_{type}", st.arrow, st.emission * 0.8f);
            var accent = LitMaterial($"Fleche_{type}_Detail", st.arrowAccent, st.emission * 0.5f, 0.6f);

            foreach (var r in arrow.GetComponentsInChildren<Renderer>(true))
                if (!(r is TrailRenderer) && !(r is LineRenderer) && !(r is ParticleSystemRenderer)) Paint(r, main, accent);

            // Toutes les flèches comptent les points (avant, seule Arrow_Base avait ArrowHit)
            GetOrAdd<ArrowHit>(arrow.gameObject);

            // Traînée : un vrai TrailRenderer sur l'enfant "Traînée", à l'arrière de la flèche
            var t = arrow.transform.Find(TrailName);
            if (t == null)
            {
                t = new GameObject(TrailName).transform;
                t.SetParent(arrow.transform, false);
            }
            t.position = arrow.grabPoint != null ? arrow.grabPoint.position : arrow.transform.position;

            var trail = GetOrAdd<TrailRenderer>(t.gameObject);
            trail.sharedMaterial = trailMat;
            trail.time = st.trailTime;
            trail.widthMultiplier = st.trailWidth;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.6f), new Keyframe(1f, 0f));
            trail.colorGradient = MakeGradient(st.trail);
            trail.minVertexDistance = 0.03f;
            trail.numCapVertices = 2;
            trail.emitting = false;
            trail.autodestruct = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            EditorUtility.SetDirty(trail);

            var at = GetOrAdd<ArrowTrail>(arrow.gameObject);
            at.trail = trail;
            at.defaultColors = MakeGradient(st.trail);
            at.defaultWidth = st.trailWidth;
            at.defaultTime = st.trailTime;
            at.sparks = SetupArrowSparks(t);
            trail.textureMode = LineTextureMode.Tile;
            trail.textureScale = new Vector2(2f, 1f);   // l'éclair se répète tous les 50 cm
            EditorUtility.SetDirty(at);
            EditorUtility.SetDirty(arrow.gameObject);
        }
    }

    // ---------- Carquois dans le dos ----------

    static void SetupQuiver(Scene scene)
    {
        var origin = All<XROrigin>(scene).FirstOrDefault();
        if (origin == null) return;

        var exemplars = ArrowExemplars(scene);
        Transform parent = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : origin.transform;

        DestroyChild(parent, QuiverName);
        var quiverGO = new GameObject(QuiverName);
        quiverGO.transform.SetParent(parent, false);
        var quiver = quiverGO.AddComponent<Quiver>();
        quiver.head = origin.Camera != null ? origin.Camera.transform : null;

        // Position de départ (le script le recolle au dos du joueur pendant le jeu)
        if (quiver.head != null)
        {
            Vector3 f = Vector3.ProjectOnPlane(quiver.head.forward, Vector3.up).normalized;
            if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
            var yaw = Quaternion.LookRotation(f);
            quiverGO.transform.SetPositionAndRotation(quiver.head.position + yaw * quiver.offsetFromHead, yaw);
        }

        // Le carquois lui-même (cuir), sans collider
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Étui";
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(quiverGO.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
        body.transform.localScale = new Vector3(0.11f, 0.22f, 0.11f);
        body.GetComponent<Renderer>().sharedMaterial = LitMaterial("Carquois", new Color(0.4f, 0.2f, 0.08f), Color.black, 0.2f);

        // Les flèches rangées : de vraies flèches, cachées jusqu'à ce qu'on les prenne
        quiver.arrows.Clear();
        foreach (var type in ArcheryTypes.All)
        {
            if (!exemplars.TryGetValue(type, out var model)) continue;
            for (int i = 0; i < ArrowsPerTypeInQuiver; i++)
            {
                var a = CopyArrow(model, $"Arrow_{type} (carquois {i + 1})", quiverGO.transform);
                AlignArrow(a, quiverGO.transform.position + quiverGO.transform.right * (i - 1) * 0.02f, Vector3.up);
                a.gameObject.SetActive(false);
                quiver.arrows.Add(a);
            }
        }
        EditorUtility.SetDirty(quiver);
    }

    // ---------- Cibles ----------

    static void FixRings(Target target, bool canRemove)
    {
        foreach (Transform ring in target.transform)
        {
            if (!ring.name.StartsWith(target.ringPrefix) || !ring.TryGetComponent(out MeshFilter mf)) continue;
            if (ring.TryGetComponent(out CapsuleCollider capsule))
            {
                if (canRemove) Object.DestroyImmediate(capsule, true);
                else { capsule.enabled = false; EditorUtility.SetDirty(capsule); }
            }
            var mc = GetOrAdd<MeshCollider>(ring.gameObject);
            mc.sharedMesh = mf.sharedMesh;
            EditorUtility.SetDirty(mc);
        }
    }

    static void FixTargetPrefabs()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<Target>(true) == null) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            foreach (var t in root.GetComponentsInChildren<Target>(true))
            {
                bool nested = PrefabUtility.IsPartOfPrefabInstance(t.gameObject);
                FixRings(t, !nested);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void SetupTargets(Scene scene)
    {
        foreach (var t in All<Target>(scene))
            FixRings(t, !PrefabUtility.IsPartOfPrefabInstance(t.gameObject));

        foreach (var mt in All<MovingTarget>(scene))
        {
            var rb = GetOrAdd<Rigidbody>(mt.gameObject);
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            EditorUtility.SetDirty(rb);
        }
    }

    static void SetupTargetSpawners(Scene scene)
    {
        foreach (var sp in All<TargetSpawner>(scene))
        {
            DestroyChild(sp.transform, TargetSpawner.PoolName);
            if (sp.targetPrefab == null) { Debug.LogWarning($"[Projet Arc] {sp.name} : pas de 'Target Prefab'."); continue; }

            var pool = new GameObject(TargetSpawner.PoolName).transform;
            pool.SetParent(sp.transform, false);

            for (int i = 0; i < Mathf.Max(1, sp.maxAlive); i++)
            {
                var src = sp.targetPrefab.gameObject;
                var go = PrefabUtility.IsPartOfPrefabAsset(src)
                    ? (GameObject)PrefabUtility.InstantiatePrefab(src, scene)
                    : Object.Instantiate(src);
                go.name = $"Cible en réserve {i + 1}";
                go.transform.SetParent(pool, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one * sp.targetScale;

                var t = go.GetComponent<Target>();
                if (sp.pointsMultiplier != 1)
                {
                    var pts = (int[])sp.targetPrefab.ringPoints.Clone();
                    for (int k = 0; k < pts.Length; k++) pts[k] *= sp.pointsMultiplier;
                    t.ringPoints = pts;
                }
                FixRings(t, false);

                if (sp.moveChance > 0f && sp.appearMode == TargetSpawner.Appear.Grossir)
                {
                    var mt = GetOrAdd<MovingTarget>(go);
                    mt.motion = MovingTarget.Motion.Auto;
                    mt.aleatoire = true;
                    var rb = GetOrAdd<Rigidbody>(go);
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                }
                EditorUtility.SetDirty(t);
                go.SetActive(false);
            }
        }
    }

    static void SetupBirdSpawners(Scene scene)
    {
        foreach (var sp in All<FlyingBirdSpawner>(scene))
        {
            DestroyChild(sp.transform, FlyingBirdSpawner.PoolName);
            if (sp.birdPrefab == null) { Debug.LogWarning($"[Projet Arc] {sp.name} : pas de 'Bird Prefab'."); continue; }

            var pool = new GameObject(FlyingBirdSpawner.PoolName).transform;
            pool.SetParent(sp.transform, false);

            sp.maxAlive = 1;   // un seul oiseau
            EditorUtility.SetDirty(sp);
            for (int i = 0; i < 1; i++)
            {
                var src = sp.birdPrefab;
                var bird = PrefabUtility.IsPartOfPrefabAsset(src)
                    ? (GameObject)PrefabUtility.InstantiatePrefab(src, scene)
                    : Object.Instantiate(src);
                bird.name = $"Oiseau en réserve {i + 1}";
                bird.transform.SetParent(pool, false);
                bird.transform.localPosition = Vector3.zero;
                bird.transform.localScale = src.transform.localScale * sp.birdScale;

                // Les scripts du pack "living birds" ne doivent pas reprendre la main : c'est le spawner qui le pilote
                foreach (var mb in bird.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb.GetType().Name.StartsWith("lb_")) { mb.enabled = false; EditorUtility.SetDirty(mb); }
                foreach (var rb in bird.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }

                SetupBirdAnimator(bird);

                var bt = GetOrAdd<BirdTarget>(bird);
                bt.points = sp.points;
                bt.hitRadius = sp.hitRadius;
                bt.reaction = BirdTarget.Reaction.SEnvole;
                bt.flyingAnimatorBool = sp.flyingAnimatorBool;
                EditorUtility.SetDirty(bt);
                bird.SetActive(false);
            }
        }
    }

    // ---------- Panneau des résultats ----------

    static void SetupResultsBoards(Scene scene)
    {
        var sm = All<ScoreManager>(scene).FirstOrDefault();
        int arrows = sm != null ? sm.arrowsPerRound : 10;
        foreach (var board in All<ResultsBoard>(scene))
        {
            board.BuildSlots(arrows);
            EditorUtility.SetDirty(board);
        }
    }

    // ---------- Chrono 5 min ----------

    static void SetupTimer(Scene scene)
    {
        var sm = All<ScoreManager>(scene).FirstOrDefault();
        foreach (var old in All<LevelTimer>(scene)) if (old.name == TimerName) Object.DestroyImmediate(old.gameObject);
        if (sm == null) return;   // pas un niveau (menu)

        var go = new GameObject(TimerName);
        SceneManager.MoveGameObjectToScene(go, scene);
        var timer = go.AddComponent<LevelTimer>();
        timer.levelDuration = 90f;
        var displays = new List<TMP_Text>();

        // 1) Montre au poignet gauche
        var origin = All<XROrigin>(scene).FirstOrDefault();
        if (origin != null)
        {
            Transform left = FindDeep(origin.transform, "Left Controller") ?? FindDeep(origin.transform, "LeftHand Controller");
            if (left != null)
            {
                DestroyChild(left, WristName);
                var watch = new GameObject(WristName).transform;
                watch.SetParent(left, false);
                watch.localPosition = new Vector3(0f, 0.035f, -0.07f);
                watch.localRotation = Quaternion.Euler(55f, 0f, 0f);

                var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
                face.name = "Cadran";
                Object.DestroyImmediate(face.GetComponent<Collider>());
                face.transform.SetParent(watch, false);
                face.transform.localPosition = new Vector3(0f, 0f, 0.003f);
                face.transform.localScale = new Vector3(0.12f, 0.045f, 1f);
                face.transform.localRotation = Quaternion.identity;
                face.GetComponent<Renderer>().sharedMaterial = LitMaterial("Cadran", new Color(0.12f, 0.07f, 0.03f), Color.black, 0.1f);

                var textGO = new GameObject("Temps", typeof(RectTransform));
                textGO.transform.SetParent(watch, false);
                var tmp = textGO.AddComponent<TextMeshPro>();
                if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
                tmp.rectTransform.sizeDelta = new Vector2(0.12f, 0.045f);
                tmp.text = "1:30";
                tmp.fontSize = 0.3f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.color = Color.white;
                displays.Add(tmp);
            }
            else Debug.LogWarning("[Projet Arc] Pas de 'Left Controller' sous l'XR Origin : pas de montre.");
        }

        // 2) Au-dessus du panneau des résultats
        foreach (var board in All<ResultsBoard>(scene))
        {
            var canvas = board.GetComponentInChildren<Canvas>(true);
            if (canvas == null) continue;
            DestroyChild(canvas.transform, BoardChronoName);
            var rt = new GameObject(BoardChronoName, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 10f);
            rt.sizeDelta = new Vector2(700f, 80f);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = "1:30";
            t.fontSize = 60;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(0, 0, 0, 255);
            t.raycastTarget = false;
            displays.Add(t);
        }

        timer.displays = displays.ToArray();
        EditorUtility.SetDirty(timer);
    }
}
