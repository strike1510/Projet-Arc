using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Suite de l'outil "Projet Arc > Configurer le projet" :
/// effets électriques du Bow_Fulminaleuse (Particle Systems, lumières de particules, traînée éclair)
/// et oiseau unique animé (Animator du pack living birds).
/// Tout est fait avec des composants Unity posés dans la scène, sans script de jeu en plus.
/// </summary>
public static partial class ProjetArcSetup
{
    const string ElecName = "Effets électriques";
    const string ArrowSparksName = "Étincelles";
    const string BirdController = "Assets/living birds/animations/birdAnimatorController.controller";
    const string DefaultBirdModel = "Assets/living birds/models/crow.FBX";

    static readonly Color ElecYellow = new Color(1f, 0.92f, 0.45f);
    static readonly Color ElecViolet = new Color(0.7f, 0.4f, 1f);

    // ---------- Textures générées (vraies images PNG dans le projet) ----------

    static Texture2D SaveTexture(string name, Texture2D tex, TextureWrapMode wrap)
    {
        string folder = MatFolder + "/Textures";
        EnsureFolder(folder);
        string path = $"{folder}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true;
        imp.wrapMode = wrap;
        imp.mipmapEnabled = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Éclair en zigzag, blanc sur fond transparent, qui se répète bout à bout (traînée en mode Tile).</summary>
    static Texture2D LightningTexture()
    {
        string path = MatFolder + "/Textures/Eclair.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int W = 256, H = 64;
        var rnd = new System.Random(7);
        var main = new float[W];
        var branch = new float[W];
        float y = H / 2f, b = H / 2f;
        for (int x = 0; x < W; x++)
        {
            y += (float)(rnd.NextDouble() * 2 - 1) * 7f;
            y = Mathf.Clamp(y, 10f, H - 10f);
            b += (float)(rnd.NextDouble() * 2 - 1) * 9f;
            b = Mathf.Clamp(b, 4f, H - 4f);
            float edge = Mathf.Min(x, W - 1 - x) / 24f;          // revient au milieu aux deux bouts : raccord parfait
            main[x] = Mathf.Lerp(H / 2f, y, Mathf.Clamp01(edge));
            branch[x] = Mathf.Lerp(main[x], b, Mathf.Clamp01(edge) * 0.8f);
        }

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var px = new Color[W * H];
        for (int x = 0; x < W; x++)
        for (int yy = 0; yy < H; yy++)
        {
            float d = Mathf.Abs(yy - main[x]);
            float core = Mathf.Exp(-d * d / (2f * 1.3f * 1.3f));
            float glow = 0.35f * Mathf.Exp(-d * d / (2f * 6f * 6f));
            float db = Mathf.Abs(yy - branch[x]);
            float br = 0.55f * Mathf.Exp(-db * db / (2f * 0.9f * 0.9f));
            px[yy * W + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(core, br) + glow));
        }
        tex.SetPixels(px);
        tex.Apply();
        return SaveTexture("Eclair", tex, TextureWrapMode.Repeat);
    }

    /// <summary>Point lumineux doux (étincelle).</summary>
    static Texture2D SparkTexture()
    {
        string path = MatFolder + "/Textures/Etincelle.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var px = new Color[S * S];
        for (int x = 0; x < S; x++)
        for (int y = 0; y < S; y++)
        {
            float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S / 2f, S / 2f)) / (S / 2f);
            float a = Mathf.Clamp01(1f - r);
            px[y * S + x] = new Color(1f, 1f, 1f, a * a * a + 0.6f * Mathf.Exp(-r * r * 40f));
        }
        tex.SetPixels(px);
        tex.Apply();
        return SaveTexture("Etincelle", tex, TextureWrapMode.Clamp);
    }

    // ---------- Matériaux additifs (URP Particles/Unlit) ----------

    static Material AdditiveMaterial(string name, Texture2D tex)
    {
        EnsureFolder(MatFolder);
        string path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            mat = src != null ? new Material(src) : new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Surface"))
        {
            // Transparent, mélange additif : la lumière s'ajoute (effet lumineux)
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material ElectricTrailMaterial() => AdditiveMaterial("Trainee_Electrique", LightningTexture());
    static Material SparkMaterial() => AdditiveMaterial("Etincelle", SparkTexture());

    // ---------- Particle Systems ----------

    static ParticleSystem NewParticles(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = SparkMaterial();
        r.trailMaterial = ElectricTrailMaterial();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    static ParticleSystem.MinMaxGradient ElecColors() => new ParticleSystem.MinMaxGradient(ElecYellow, ElecViolet);

    static void FadeOut(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    static void ZigZagTrails(ParticleSystem ps, float lifetime, float width)
    {
        var tr = ps.trails;
        tr.enabled = true;
        tr.mode = ParticleSystemTrailMode.PerParticle;
        tr.ratio = 1f;
        tr.lifetime = lifetime;
        tr.minVertexDistance = 0.003f;
        tr.dieWithParticles = true;
        tr.inheritParticleColor = true;
        tr.sizeAffectsWidth = false;
        tr.widthOverTrail = new ParticleSystem.MinMaxCurve(width, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
    }

    static void Crackle(ParticleSystem ps, float strength, float frequency)
    {
        var n = ps.noise;
        n.enabled = true;
        n.strength = strength;
        n.frequency = frequency;
        n.scrollSpeed = 4f;
        n.damping = true;
        n.octaveCount = 2;
        n.quality = ParticleSystemNoiseQuality.High;
    }

    /// <summary>Le plus gros MeshRenderer de l'arc (son modèle), sans les flèches ni l'aide à la visée.</summary>
    static Renderer BowBody(BowController bow)
    {
        return bow.GetComponentsInChildren<Renderer>(true)
            .Where(r => (r is MeshRenderer || r is SkinnedMeshRenderer)
                        && r.GetComponentInParent<ArrowController>(true) == null
                        && !IsUnder(r.transform, AimName) && !IsUnder(r.transform, ElecName))
            .OrderByDescending(r => r.bounds.size.sqrMagnitude)
            .FirstOrDefault();
    }

    /// <summary>Les particules qui naissent sur un mesh ont besoin qu'il soit lisible (Read/Write).</summary>
    static void MakeReadable(Renderer body)
    {
        Mesh mesh = body is SkinnedMeshRenderer smr ? smr.sharedMesh : body.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null || mesh.isReadable) return;
        var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)) as ModelImporter;
        if (imp == null || imp.isReadable) return;
        imp.isReadable = true;
        imp.SaveAndReimport();
    }

    static void ShapeOnBow(ParticleSystem ps, Renderer body, ParticleSystemMeshShapeType where)
    {
        var sh = ps.shape;
        sh.enabled = true;
        if (body is SkinnedMeshRenderer smr) { sh.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer; sh.skinnedMeshRenderer = smr; }
        else if (body is MeshRenderer mr) { sh.shapeType = ParticleSystemShapeType.MeshRenderer; sh.meshRenderer = mr; }
        sh.meshShapeType = where;
        sh.normalOffset = 0.004f;
    }

    /// <summary>Bow_Fulminaleuse : arcs électriques qui courent sur l'arc, gerbes d'étincelles, lueur.</summary>
    static void SetupBowElectricity(BowController bow, bool on)
    {
        DestroyChild(bow.transform, ElecName);
        if (!on)
        {
            var g = bow.GetComponent<XRGrabInteractable>();
            if (g != null) { RemoveSetActiveListeners(g.selectEntered); RemoveSetActiveListeners(g.selectExited); }
            return;
        }

        var body = BowBody(bow);
        if (body == null) { Debug.LogWarning($"[Projet Arc] {bow.name} : pas de modèle pour les effets électriques."); return; }
        MakeReadable(body);
        body = BowBody(bow);   // après réimport

        var root = new GameObject(ElecName).transform;
        root.SetParent(bow.transform, false);

        // Modèle de lumière pour le module "Lights" des particules (les éclairs éclairent autour d'eux)
        var lightGO = new GameObject("Lumière des éclairs (modèle)");
        lightGO.transform.SetParent(root, false);
        var flash = lightGO.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.range = 0.9f;
        flash.intensity = 2.5f;
        flash.color = ElecYellow;
        flash.shadows = LightShadows.None;
        lightGO.SetActive(false);

        // 1) Arcs électriques : petits filaments qui zigzaguent sur l'arc
        var arcs = NewParticles(root, "Arcs électriques");
        var m = arcs.main;
        m.loop = true; m.playOnAwake = true; m.duration = 1f;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
        m.startColor = ElecColors();
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.scalingMode = ParticleSystemScalingMode.Shape;
        m.maxParticles = 80;
        var em = arcs.emission; em.rateOverTime = 24f;
        ShapeOnBow(arcs, body, ParticleSystemMeshShapeType.Triangle);
        Crackle(arcs, 1.2f, 7f);
        ZigZagTrails(arcs, 0.7f, 0.01f);
        FadeOut(arcs);
        var lights = arcs.lights;
        lights.enabled = true;
        lights.light = flash;
        lights.ratio = 0.15f;
        lights.maxLights = 3;
        lights.useParticleColor = true;
        lights.sizeAffectsRange = false;
        lights.alphaAffectsIntensity = true;

        // 2) Gerbes d'étincelles qui jaillissent par à-coups
        var sparks = NewParticles(root, "Gerbes d'étincelles");
        m = sparks.main;
        m.loop = true; m.playOnAwake = true; m.duration = 1f;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.005f, 0.011f);
        m.startColor = new ParticleSystem.MinMaxGradient(Color.white, ElecYellow);
        m.gravityModifier = 0.6f;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.scalingMode = ParticleSystemScalingMode.Shape;
        m.maxParticles = 60;
        em = sparks.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(4, 10), 0, 0.3f) { probability = 0.6f },
        });
        ShapeOnBow(sparks, body, ParticleSystemMeshShapeType.Vertex);
        FadeOut(sparks);
        var sr = sparks.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Stretch;
        sr.velocityScale = 0.04f;
        sr.lengthScale = 2f;

        // 3) Lueur violette permanente autour de l'arc
        var glow = new GameObject("Lueur");
        glow.transform.SetParent(root, false);
        glow.transform.position = body.bounds.center;
        var gl = glow.AddComponent<Light>();
        gl.type = LightType.Point;
        gl.range = 1.2f;
        gl.intensity = 0.7f;
        gl.color = ElecViolet;
        gl.shadows = LightShadows.None;

        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) ps.Play();

        // Effets seulement quand l'arc est en main : sans script, avec les événements de l'XR Grab Interactable
        // (Inspector > XR Grab Interactable > Interactable Events > Select Entered / Select Exited → GameObject.SetActive)
        root.gameObject.SetActive(false);
        WireHeldEffects(bow, root.gameObject);
    }

    static void WireHeldEffects(BowController bow, GameObject effects)
    {
        var grab = bow.GetComponent<XRGrabInteractable>();
        if (grab == null) { Debug.LogWarning($"[Projet Arc] {bow.name} : pas d'XR Grab Interactable, les effets restent éteints."); return; }

        RemoveSetActiveListeners(grab.selectEntered);
        RemoveSetActiveListeners(grab.selectExited);
        UnityEventTools.AddBoolPersistentListener(grab.selectEntered, effects.SetActive, true);
        UnityEventTools.AddBoolPersistentListener(grab.selectExited, effects.SetActive, false);
        EditorUtility.SetDirty(grab);
    }

    /// <summary>Enlève les anciens branchements vers "Effets électriques" (ou vers un objet supprimé) avant d'en remettre.</summary>
    static void RemoveSetActiveListeners(UnityEngine.Events.UnityEventBase evt)
    {
        for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            var target = evt.GetPersistentTarget(i);
            bool ours = evt.GetPersistentMethodName(i) == "SetActive"
                        && (target == null || (target is GameObject go && go.name == ElecName));
            if (ours) UnityEventTools.RemovePersistentListener(evt, i);
        }
    }

    /// <summary>
    /// Étincelles en vol, sur chaque flèche : émission "par distance" (seulement quand la flèche avance).
    /// Coupées par défaut : ArrowTrail les allume quand l'arc qui tire a "Trail Sparks" (Bow_Fulminaleuse).
    /// </summary>
    static ParticleSystem SetupArrowSparks(Transform trailObject)
    {
        DestroyChild(trailObject, ArrowSparksName);
        var ps = NewParticles(trailObject, ArrowSparksName);
        var m = ps.main;
        m.loop = true; m.playOnAwake = true; m.duration = 1f;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
        m.startColor = ElecColors();
        m.gravityModifier = 0.2f;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.scalingMode = ParticleSystemScalingMode.Shape;
        m.maxParticles = 150;
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.rateOverDistance = 35f;
        em.enabled = false;
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.02f;
        Crackle(ps, 1.5f, 8f);
        ZigZagTrails(ps, 0.5f, 0.012f);
        FadeOut(ps);
        return ps;
    }

    // ---------- Un seul oiseau ----------

    /// <summary>
    /// Supprime tous les oiseaux posés à la main (pack living birds) et les spawners en trop :
    /// il reste UN oiseau par niveau, piloté par un seul FlyingBirdSpawner (il arrive en volant, se pose, repart).
    /// </summary>
    static void KeepSingleBird(Scene scene)
    {
        GameObject model = null;

        // Spawner posé SUR un oiseau (le modèle resterait visible, immobile) : on le déplace sur un objet vide
        foreach (var sp in All<FlyingBirdSpawner>(scene))
        {
            var host = sp.gameObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(host)) continue;
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(host);
            if (!path.Contains("/living birds/")) continue;

            var go = new GameObject("OiseauEnVol");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = host.transform.position;
            var copy = go.AddComponent<FlyingBirdSpawner>();
            EditorUtility.CopySerialized(sp, copy);
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(host);
            Debug.Log($"[Projet Arc] {scene.name} : le spawner d'oiseaux était sur '{root.name}' : déplacé sur 'OiseauEnVol', oiseau immobile supprimé.");
            Object.DestroyImmediate(root);
        }

        // Oiseaux posés directement dans la scène (ex. "cardinal")
        var loose = All<Transform>(scene)
            .Select(t => t.gameObject)
            .Where(go => PrefabUtility.IsOutermostPrefabInstanceRoot(go)
                         && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go).Contains("/living birds/")
                         && go.GetComponentInParent<FlyingBirdSpawner>(true) == null)
            .ToList();
        foreach (var go in loose)
        {
            if (model == null) model = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go));
            Debug.Log($"[Projet Arc] {scene.name} : oiseau '{go.name}' supprimé (un seul oiseau par niveau).");
            Object.DestroyImmediate(go);
        }

        // Un seul spawner
        var spawners = All<FlyingBirdSpawner>(scene);
        for (int i = 1; i < spawners.Count; i++)
        {
            var sp = spawners[i];
            Debug.Log($"[Projet Arc] {scene.name} : spawner d'oiseaux '{sp.name}' en trop supprimé.");
            if (sp.GetComponents<Component>().Length <= 2) Object.DestroyImmediate(sp.gameObject);
            else { DestroyChild(sp.transform, FlyingBirdSpawner.PoolName); Object.DestroyImmediate(sp); }
        }

        // Pas de spawner dans un niveau : on en pose un (avec l'oiseau qui était dans la scène)
        if (spawners.Count == 0 && All<ScoreManager>(scene).Count > 0)
        {
            var go = new GameObject("OiseauEnVol");
            SceneManager.MoveGameObjectToScene(go, scene);
            var sp = go.AddComponent<FlyingBirdSpawner>();
            sp.birdPrefab = model != null ? model : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBirdModel);
            Debug.Log($"[Projet Arc] {scene.name} : oiseau en vol ajouté ({(sp.birdPrefab != null ? sp.birdPrefab.name : "aucun modèle")}).");
        }
    }

    /// <summary>Les modèles FBX du pack n'ont pas de contrôleur d'animation : on branche celui du pack (vol, atterrissage, sol).</summary>
    static void SetupBirdAnimator(GameObject bird)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BirdController);
        if (ctrl == null) { Debug.LogWarning($"[Projet Arc] Contrôleur introuvable : {BirdController}"); return; }

        var anims = bird.GetComponentsInChildren<Animator>(true);
        if (anims.Length == 0) anims = new[] { bird.AddComponent<Animator>() };
        foreach (var a in anims)
        {
            a.runtimeAnimatorController = ctrl;
            a.applyRootMotion = false;
            a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(a);
        }
    }
}
