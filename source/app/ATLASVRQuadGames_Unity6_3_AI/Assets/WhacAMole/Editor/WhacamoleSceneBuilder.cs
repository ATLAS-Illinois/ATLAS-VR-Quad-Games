using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Whacamole;
using Whacamole.Controls;
using Whacamole.Events;
using Whacamole.Gameplay;
using Whacamole.UI;

/// <summary>
/// Builds the visible whac-a-mole scene: floor, board with 9 holes, nuts, scoreboard,
/// and a hammer attached to each VR controller. Safe to run again; it replaces what it built before.
/// Menu: Whacamole > Build Scene
/// </summary>
public static class WhacamoleSceneBuilder
{
    // Imported into ATLAS VR Quad Games: everything lives under Assets/WhacAMole.
    private const string GameRoot = "Assets/WhacAMole";
    private const string ArtRoot = GameRoot + "/Art";
    private const string MatFolder = ArtRoot + "/Materials";
    private const string PrefabFolder = ArtRoot + "/Prefabs";
    private const string EventFolder = GameRoot + "/Data/Events";

    // The only scene this builder is meant to modify.
    public const string SceneName = "WhacAMole";

    private const float BoardTopHeight = 0.85f; // meters above the floor
    private const float BoardDistance = 0.6f;   // meters in front of the player

    [MenuItem("Whacamole/Build Scene")]
    public static void Build()
    {
        // Safety: this deletes/rebuilds objects in the *active* scene. Don't let it run on
        // QuadProjectAlexanderLau or any other Quad Games scene by accident.
        if (EditorSceneManager.GetActiveScene().name != SceneName)
        {
            EditorUtility.DisplayDialog("Whacamole",
                $"Open Assets/Scenes/{SceneName}.unity first. Build Scene only modifies the whac-a-mole scene.", "OK");
            return;
        }

        EnsureFolder("Assets", "WhacAMole");
        EnsureFolder(GameRoot, "Art");
        EnsureFolder(ArtRoot, "Materials");
        EnsureFolder(ArtRoot, "Prefabs");

        // ---------- Materials ----------
        Material wood       = GetOrCreateMaterial("BoardWood",    new Color(0.55f, 0.36f, 0.20f));
        Material boardTop   = GetOrCreateMaterial("BoardTop",     new Color(0.30f, 0.62f, 0.28f));
        Material hole       = GetOrCreateMaterial("Hole",         new Color(0.08f, 0.06f, 0.05f));
        Material nutBody    = GetOrCreateMaterial("NutBody",      new Color(0.72f, 0.45f, 0.20f));
        Material nutCap     = GetOrCreateMaterial("NutCap",       new Color(0.36f, 0.22f, 0.10f));
        Material handle     = GetOrCreateMaterial("HammerHandle", new Color(0.85f, 0.70f, 0.45f));
        Material head       = GetOrCreateMaterial("HammerHead",   new Color(0.85f, 0.15f, 0.12f));
        Material floor      = GetOrCreateMaterial("Floor",        new Color(0.45f, 0.50f, 0.45f));
        Material boardFrame = GetOrCreateMaterial("Scoreboard",   new Color(0.12f, 0.12f, 0.16f));

        // ---------- Prefabs ----------
        GameObject nutPrefab = BuildNutPrefab(nutBody, nutCap);
        GameObject hammerPrefab = BuildHammerPrefab(handle, head);

        // ---------- Scene ----------
        DestroyIfExists("Arena");
        DestroyIfExists("Hammer_Left");
        DestroyIfExists("Hammer_Right");

        var arena = new GameObject("Arena");

        // Floor
        GameObject floorGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floorGo.name = "Floor";
        floorGo.transform.SetParent(arena.transform, false);
        floorGo.transform.localScale = new Vector3(2f, 1f, 2f);
        SetMat(floorGo, floor);

        // Board (table with holes on top)
        var board = new GameObject("WhackBoard");
        board.transform.SetParent(arena.transform, false);
        board.transform.localPosition = new Vector3(0f, 0f, BoardDistance);

        const float boardW = 1.0f, boardD = 0.7f, topThickness = 0.05f;
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Base";
        body.transform.SetParent(board.transform, false);
        body.transform.localPosition = new Vector3(0f, (BoardTopHeight - topThickness) / 2f, 0f);
        body.transform.localScale = new Vector3(boardW, BoardTopHeight - topThickness, boardD);
        SetMat(body, wood);
        // No solid collider on the table itself: the player's body capsule would stop well short of the edge.
        // BoardLayout adds an invisible PlayerBlocker instead, pulled in by the body radius, at runtime.
        Object.DestroyImmediate(body.GetComponent<Collider>());

        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.name = "Top";
        top.transform.SetParent(board.transform, false);
        top.transform.localPosition = new Vector3(0f, BoardTopHeight - topThickness / 2f, 0f);
        top.transform.localScale = new Vector3(boardW + 0.04f, topThickness, boardD + 0.04f);
        SetMat(top, boardTop);
        Object.DestroyImmediate(top.GetComponent<Collider>());

        // 3x3 holes with a nut in each
        var holes = new GameObject("Holes");
        holes.transform.SetParent(board.transform, false);
        const float spacingX = 0.3f, spacingZ = 0.2f;
        int i = 0;
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                var pos = new Vector3((col - 1) * spacingX, BoardTopHeight, (row - 1) * spacingZ);

                GameObject h = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                h.name = $"Hole_{i}";
                h.transform.SetParent(holes.transform, false);
                h.transform.localPosition = pos + new Vector3(0f, 0.001f, 0f);
                h.transform.localScale = new Vector3(0.14f, 0.002f, 0.14f);
                Object.DestroyImmediate(h.GetComponent<Collider>());
                SetMat(h, hole);

                var nut = (GameObject)PrefabUtility.InstantiatePrefab(nutPrefab, holes.transform);
                nut.name = $"Nut_{i}";
                var nutSo = new SerializedObject(nut.GetComponent<Nut>());
                nutSo.FindProperty("holeIndex").intValue = i;
                nutSo.ApplyModifiedPropertiesWithoutUndo();
                // Home = hole center, flush with the table top. The Nut hides itself below this in Play mode.
                nut.transform.localPosition = pos;
                i++;
            }
        }

        // Scoreboard behind the board
        var scoreboard = new GameObject("Scoreboard");
        scoreboard.transform.SetParent(arena.transform, false);
        scoreboard.transform.localPosition = new Vector3(0f, 1.6f, BoardDistance + 0.9f);

        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Panel";
        panel.transform.SetParent(scoreboard.transform, false);
        panel.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        panel.transform.localScale = new Vector3(1.0f, 0.45f, 0.04f);
        SetMat(panel, boardFrame);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(scoreboard.transform, false);
        var textMesh = textGo.AddComponent<TextMesh>();
        textMesh.text = "Score 0\nLives 3   Combo 0";
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.02f;
        textMesh.fontSize = 64;
        textMesh.color = Color.white;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            textMesh.font = font;
            textGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        var sb = scoreboard.AddComponent<Scoreboard>();
        var sbSo = new SerializedObject(sb);
        sbSo.FindProperty("text").objectReferenceValue = textMesh;
        sbSo.FindProperty("scoreChangedChannel").objectReferenceValue = LoadEvent("ScoreChanged");
        sbSo.FindProperty("livesChangedChannel").objectReferenceValue = LoadEvent("LivesChanged");
        sbSo.FindProperty("comboChangedChannel").objectReferenceValue = LoadEvent("ComboChanged");
        sbSo.FindProperty("gameStartedChannel").objectReferenceValue = LoadEvent("GameStarted");
        sbSo.FindProperty("gameOverChannel").objectReferenceValue = LoadEvent("GameOver");
        sbSo.ApplyModifiedPropertiesWithoutUndo();

        // ---------- Nut spawner ----------
        var spawnerGo = new GameObject("NutSpawner");
        spawnerGo.transform.SetParent(arena.transform, false);
        var spawner = spawnerGo.AddComponent<NutSpawner>();
        var spSo = new SerializedObject(spawner);
        spSo.FindProperty("gameStartedChannel").objectReferenceValue = LoadEvent("GameStarted");
        spSo.FindProperty("gameOverChannel").objectReferenceValue = LoadEvent("GameOver");
        spSo.FindProperty("nutHitChannel").objectReferenceValue = LoadEvent("NutHit");
        spSo.FindProperty("nutMissedChannel").objectReferenceValue = LoadEvent("NutMissed");
        spSo.ApplyModifiedPropertiesWithoutUndo();

        // ---------- Hammers on stands beside the table (grab with the grip button) ----------
        PlaceHammerOnStand(hammerPrefab, Hand.Left, arena.transform, wood);
        PlaceHammerOnStand(hammerPrefab, Hand.Right, arena.transform, wood);

        var grabberGo = new GameObject("HammerGrabber");
        grabberGo.transform.SetParent(arena.transform, false);
        grabberGo.AddComponent<HammerGrabber>();

        Transform left = FindFirst("LeftHandAnchor", "LeftControllerAnchor");
        Transform right = FindFirst("RightHandAnchor", "RightControllerAnchor");

        // Point the InputManager at the hand anchors
        var input = Object.FindFirstObjectByType<InputManager>();
        if (input != null)
        {
            var so = new SerializedObject(input);
            Transform lh = FindFirst("LeftHandAnchor");
            Transform rh = FindFirst("RightHandAnchor");
            if (lh != null) so.FindProperty("leftController").objectReferenceValue = lh;
            if (rh != null) so.FindProperty("rightController").objectReferenceValue = rh;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Make sure the headset height is measured from the floor, so the board is at waist height.
        SetFloorLevelTracking();

        // Without a VR rig, put the normal camera where the player's head would be, looking at the board.
        if (left == null || right == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 1.5f, -0.25f);
                cam.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
                cam.nearClipPlane = 0.05f;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        if (left == null || right == null)
            Debug.LogWarning("[Whacamole] Built the arena, but no VR controller anchors were found, so the hammers can't be grabbed yet. Run Meta > Simplified VR Setup, then run Whacamole > Build Scene again.");
        else
            Debug.Log("[Whacamole] Scene built: arena, 9 nuts that pop out of the holes, scoreboard, and two hammers on stands (grab with the grip button).");
    }

    // ---------- Prefab builders ----------

    private static GameObject BuildNutPrefab(Material bodyMat, Material capMat)
    {
        var root = new GameObject("Nut");

        GameObject bodyGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bodyGo.name = "Body";
        bodyGo.transform.SetParent(root.transform, false);
        bodyGo.transform.localPosition = new Vector3(0f, 0.045f, 0f);
        bodyGo.transform.localScale = new Vector3(0.09f, 0.11f, 0.09f);
        SetMat(bodyGo, bodyMat);

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cap.name = "Cap";
        cap.transform.SetParent(root.transform, false);
        cap.transform.localPosition = new Vector3(0f, 0.09f, 0f);
        cap.transform.localScale = new Vector3(0.1f, 0.045f, 0.1f);
        Object.DestroyImmediate(cap.GetComponent<Collider>());
        SetMat(cap, capMat);

        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "Stem";
        stem.transform.SetParent(root.transform, false);
        stem.transform.localPosition = new Vector3(0f, 0.115f, 0f);
        stem.transform.localScale = new Vector3(0.012f, 0.012f, 0.012f);
        Object.DestroyImmediate(stem.GetComponent<Collider>());
        SetMat(stem, capMat);

        // Hit detection: trigger collider on the body + kinematic Rigidbody on the root.
        bodyGo.GetComponent<Collider>().isTrigger = true;
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        root.AddComponent<Nut>();

        return SavePrefab(root, PrefabFolder + "/Nut.prefab");
    }

    private static GameObject BuildHammerPrefab(Material handleMat, Material headMat)
    {
        var root = new GameObject("Hammer");

        // Handle runs forward out of the fist, the head sits at the far end.
        GameObject handleGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handleGo.name = "Handle";
        handleGo.transform.SetParent(root.transform, false);
        handleGo.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        handleGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        handleGo.transform.localScale = new Vector3(0.03f, 0.15f, 0.03f); // 30cm long
        Object.DestroyImmediate(handleGo.GetComponent<Collider>());
        SetMat(handleGo, handleMat);

        // Head: a short fat cylinder standing upright so its flat face strikes downward.
        GameObject headGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        headGo.name = "Head";
        headGo.transform.SetParent(root.transform, false);
        headGo.transform.localPosition = new Vector3(0f, 0f, 0.28f);
        headGo.transform.localScale = new Vector3(0.09f, 0.07f, 0.09f); // 14cm tall
        SetMat(headGo, headMat);
        Object.DestroyImmediate(headGo.GetComponent<Collider>());
        var headCol = headGo.AddComponent<CapsuleCollider>();
        headCol.direction = 1;
        headCol.radius = 0.5f;
        headCol.height = 2f;
        headCol.isTrigger = true;

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var hammer = root.AddComponent<Hammer>();
        hammer.Setup(Hand.Right, headCol);

        return SavePrefab(root, PrefabFolder + "/Hammer.prefab");
    }

    /// <summary>
    /// Puts a hammer lying on a small stand in front of one of the table's front corners, angled 40°
    /// with the handle toward the player at hip height, so it's easy to pick up with the grip button.
    /// It returns here when let go.
    /// </summary>
    private static void PlaceHammerOnStand(GameObject prefab, Hand hand, Transform parent, Material standMat)
    {
        float side = hand == Hand.Left ? -1f : 1f;
        Quaternion angle = Quaternion.Euler(0f, side * 40f, 0f);              // head points forward and outward
        var gripPoint = new Vector3(side * 0.5f, BoardTopHeight + 0.075f, 0f);  // handle end, nearest the player

        // Stand: a narrow block under the hammer's length. Keeps its collider so the player can't walk through it;
        // it's off to the side, so it doesn't stop you reaching the table.
        GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stand.name = hand == Hand.Left ? "HammerStand_Left" : "HammerStand_Right";
        stand.transform.SetParent(parent, false);
        stand.transform.localPosition = new Vector3(gripPoint.x, BoardTopHeight / 2f, gripPoint.z) + angle * new Vector3(0f, 0f, 0.16f);
        stand.transform.localRotation = angle;
        stand.transform.localScale = new Vector3(0.12f, BoardTopHeight, 0.34f);
        SetMat(stand, standMat);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = hand == Hand.Left ? "Hammer_Left" : "Hammer_Right";
        go.transform.localPosition = gripPoint;
        go.transform.localRotation = angle;

        var hammer = go.GetComponent<Hammer>();
        var so = new SerializedObject(hammer);
        so.FindProperty("hand").enumValueIndex = (int)hand;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Helpers ----------

    private static void SetFloorLevelTracking()
    {
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null || mb.GetType().Name != "OVRManager") continue;
            var so = new SerializedObject(mb);
            var prop = so.FindProperty("_trackingOriginType");
            if (prop != null)
            {
                prop.enumValueIndex = 1; // FloorLevel
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static Transform FindFirst(params string[] names)
    {
        foreach (string n in names)
        {
            GameObject go = GameObject.Find(n);
            if (go != null) return go.transform;
        }
        return null;
    }

    private static void DestroyIfExists(string name)
    {
        GameObject go = GameObject.Find(name);
        while (go != null)
        {
            Object.DestroyImmediate(go);
            go = GameObject.Find(name);
        }
    }

    private static Object LoadEvent(string name) =>
        AssetDatabase.LoadAssetAtPath<Object>($"{EventFolder}/{name}.asset");

    private static void SetMat(GameObject go, Material m) =>
        go.GetComponent<Renderer>().sharedMaterial = m;

    private static GameObject SavePrefab(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Material GetOrCreateMaterial(string name, Color color)
    {
        string path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            // Quad Games uses the Built-in Render Pipeline (the original project was URP).
            Shader shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }
}
