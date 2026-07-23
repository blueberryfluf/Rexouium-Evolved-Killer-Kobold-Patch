using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class RexEvolvedCarrierPatcher : EditorWindow
{
    const string ShowPath = "Assets/VoreSystem/Animations/CarrierAnims/CarrierShow.anim";
    const string HidePath = "Assets/VoreSystem/Animations/CarrierAnims/CarrierHide.anim";
    const string GulpPath = "Assets/VoreSystem/Animations/CarrierAnims/CarrierGulp.anim";
    const string MawOpenPath = "Assets/VoreSystem/Animations/CarrierAnims/CarrierMawOpen.anim";
    const string MawClosedPath = "Assets/VoreSystem/Animations/CarrierAnims/CarrierMawClosed.anim";
    const string PoseballUpPath = "Assets/VoreSystem/Animations/CarrierAnims/CarrierPoseballUp.anim";
    const string BellyDelayPath = "Assets/VoreSystem/Animations/CarrierAnims/BellyFillDelay.anim";
    const string VoreFxPath = "Assets/VoreSystem/VoreFX.controller";
    const string CarrierMenuPath = "Assets/VoreSystem/Submenus/Carrier Menu.asset";
    /// <summary>Gulp clip length (~0.85s) + settle padding so delay starts after swallow.</summary>
    const float GulpApproxSeconds = 0.9f;
    /// <summary>Seconds to wait after gulp finishes before stomach size / belly apply.</summary>
    const float BellyAfterGulpSeconds = 0.5f;

    const string AuthorName = "blueberry";
    const string Version = "0.1";
    const string GithubUrl = "https://github.com/blueberryfluf";
    const string DiscordUrl = "https://discord.gg/2s94q7hebm";
    const string InstagramUrl = "https://www.instagram.com/blueberry_fluf/";

    const string BodyPath = "Body";

    static readonly string[] MawOpenShapes = { "Jaw_Open", "mouthFunnel", "CheekWidth", "Tongue_Out" };
    static readonly string[] MawClosedShapes =
    {
        "Jaw_Open", "mouthFunnel", "CheekWidth", "Tongue_Out",
        "Tongue_Down", "Cheek_Suck", "NeckThickness", "NeckThickness2",
        "Weight", "TorsoThickness", "BarrelChest"
    };

    static readonly Dictionary<string, float> BellyFillHold = new Dictionary<string, float>
    {
        { "Weight", 75f },
        { "TorsoThickness", 70f },
        { "BarrelChest", 35f },
    };

    string status = "Pick your avatar base, then click Patch.";
    GameObject targetAvatar;
    Vector2 logScroll;

    const string IconsFolder = "Assets/Blueberrys_Flufs_Patcher/Editor/Icons";
    Texture2D iconCommunity;

    [InitializeOnLoadMethod]
    static void AutoApplyPendingBellyDelayOnce()
    {
        // After script reload, apply belly-delay gate once if Unity has the project open.
        const string key = "RexCarrierPatch_BellyDelay_v05";
        if (SessionState.GetBool(key, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(key, false)) return;
            try
            {
                GateStomachSizeUntilGulp();
                // Also strip gulp belly fill so size can't race the swallow.
                var gulp = AssetDatabase.LoadAssetAtPath<AnimationClip>(GulpPath);
                if (gulp != null)
                {
                    SetBlendshapeCurve(gulp, "Weight", new[] { Key(0.00f, 0f), Key(0.85f, 0f) });
                    SetBlendshapeCurve(gulp, "TorsoThickness", new[] { Key(0.00f, 0f), Key(0.85f, 0f) });
                    SetBlendshapeCurve(gulp, "BarrelChest", new[] { Key(0.00f, 0f), Key(0.85f, 0f) });
                    EditorUtility.SetDirty(gulp);
                }
                AssetDatabase.SaveAssets();
                SessionState.SetBool(key, true);
                Debug.Log("[Rex Carrier Patch] Auto-applied: belly waits ~0.5s after gulp.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Rex Carrier Patch] Auto-apply skipped: " + e.Message);
            }
        };
    }

    [MenuItem("Tools/BlueBerry/Rexouium Evolved Carrier Patch")]
    public static void ShowWindow()
    {
        var window = GetWindow<RexEvolvedCarrierPatcher>("Rexouium Carrier Patch v" + Version);
        window.minSize = new Vector2(500, 620);
        window.Show();
    }

    /// <summary>For Unity -executeMethod / batchmode.</summary>
    public static void ApplyPatchNow()
    {
        var msg = RunPatch(null);
        Debug.Log("[Rex Carrier Patch] " + msg);
    }

    void OnEnable()
    {
        EnsureIconImportSettings();
        iconCommunity = LoadIcon("community.png");
    }

    static void EnsureIconImportSettings()
    {
        string[] files = { "github.png", "discord.png", "instagram.png", "community.png" };
        bool refreshed = false;
        foreach (var file in files)
        {
            var path = IconsFolder + "/" + file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            if (importer.textureType == TextureImporterType.GUI &&
                !importer.mipmapEnabled &&
                importer.maxTextureSize <= 64)
                continue;

            importer.textureType = TextureImporterType.GUI;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 64;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            refreshed = true;
        }

        if (refreshed)
            AssetDatabase.Refresh();
    }

    static Texture2D LoadIcon(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(IconsFolder + "/" + fileName);
    }

    void OnGUI()
    {
        var title = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            wordWrap = true,
        };
        var credit = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold,
        };
        var section = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };

        GUILayout.Space(10);
        GUILayout.Label("Rexouium Evolved · Killer Kobold", title);
        GUILayout.Label("Carrier Patch", title);
        GUI.color = new Color(0.55f, 0.85f, 1f);
        GUILayout.Label("by " + AuthorName + "   ·   v" + Version, credit);
        GUI.color = Color.white;
        GUILayout.Space(10);

        GUILayout.Label("Avatar Base", section);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            targetAvatar = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Target Avatar", "Drag your Rexouium Evolved avatar root (scene or prefab)."),
                targetAvatar,
                typeof(GameObject),
                true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Hierarchy Selection", GUILayout.Height(24)))
                {
                    if (Selection.activeGameObject != null)
                        targetAvatar = Selection.activeGameObject;
                    else
                        status = "Nothing selected in the Hierarchy.";
                }
                if (GUILayout.Button("Clear", GUILayout.Width(70), GUILayout.Height(24)))
                    targetAvatar = null;
            }

            var desc = FindAvatarDescriptor(targetAvatar);
            if (targetAvatar == null)
            {
                EditorGUILayout.HelpBox("Select your avatar root first — don’t leave this empty.", MessageType.Warning);
            }
            else if (desc == null)
            {
                EditorGUILayout.HelpBox(
                    "No VRC Avatar Descriptor on \"" + targetAvatar.name + "\". Pick the avatar root.",
                    MessageType.Error);
            }
            else
            {
                string hint = DescribeAvatarTarget(targetAvatar, desc);
                EditorGUILayout.HelpBox(hint, MessageType.Info);
            }
        }

        GUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "REQUIREMENTS\n" +
            "• Avatar: Rexouium Evolved\n" +
            "• System: Killer Kobold vore\n" +
            "Don’t run this on a random Rex base — it won’t match.",
            MessageType.Warning);

        GUILayout.Space(6);
        GUILayout.Label("Version Log  ·  v" + Version, section);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.MinHeight(120), GUILayout.MaxHeight(170));
            var bullet = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true, fontSize = 11 };
            GUILayout.Label(
                "<b>What’s in this patch</b>\n" +
                "• Smooth maw open/close (Jaw_Open + funnel/cheeks/tongue)\n" +
                "• Gulp: jaw/tongue/neck — belly fills ~0.5s after gulp\n" +
                "• Stomach Size 0–6 / Wumbo → body belly (only after gulp)\n" +
                "• Burps + lip sync → Jaw_Open (on your selected avatar)\n" +
                "• Mute broken Stomach Churn (missing on Rexouium)\n" +
                "• Fix BellySeat path + rename Move Seat → Gulp",
                bullet);
            EditorGUILayout.EndScrollView();
        }

        GUILayout.Space(12);
        GUILayout.Label("Apply", section);

        var patchStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            fixedHeight = 44,
        };
        bool canPatch = FindAvatarDescriptor(targetAvatar) != null;
        using (new EditorGUI.DisabledScope(!canPatch))
        {
            GUI.backgroundColor = canPatch ? new Color(0.35f, 0.75f, 0.45f) : Color.gray;
            if (GUILayout.Button("Patch Avatar", patchStyle))
            {
                try { status = RunPatch(targetAvatar); }
                catch (System.Exception e)
                {
                    status = "Patch failed: " + e.Message;
                    Debug.LogException(e);
                }
            }
            GUI.backgroundColor = Color.white;
        }

        GUILayout.Space(6);
        bool ok = status != null && status.IndexOf("applied", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool fail = status != null && status.IndexOf("failed", System.StringComparison.OrdinalIgnoreCase) >= 0;
        EditorGUILayout.HelpBox(
            status,
            fail ? MessageType.Error : ok ? MessageType.Info : MessageType.None);

        GUILayout.Space(8);
        DrawSocialFooter();
    }

    // Dev / debug only — not shown in the patch window.
    [MenuItem("Tools/BlueBerry/Dev - Debug/Export Rexouium Patch")]
    public static void ExportUnityPackage()
    {
        ExportProdPackage(promptForPath: true);
    }

    [MenuItem("Tools/BlueBerry/Dev - Debug/Export Prod to Downloads")]
    public static void ExportProdToDownloadsMenu()
    {
        ExportProdToDownloads();
        string path = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads",
            "BlueBerry-RexouiumCarrierPatch-v" + Version + ".unitypackage");
        if (File.Exists(path))
            EditorUtility.RevealInFinder(path);
    }

    /// <summary>Silent prod export → Downloads (+ Desktop copy).</summary>
    public static void ExportProdToDownloads()
    {
        ExportProdPackage(promptForPath: false);
    }

    static void ExportProdPackage(bool promptForPath)
    {
        string defaultName = "BlueBerry-RexouiumCarrierPatch-v" + Version + ".unitypackage";
        string downloadsDir = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads");
        string path = Path.Combine(downloadsDir, defaultName);

        if (promptForPath)
        {
            path = EditorUtility.SaveFilePanel(
                "Dev — Export Rexouium Patch",
                downloadsDir,
                defaultName,
                "unitypackage");
            if (string.IsNullOrEmpty(path))
                return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Blueberrys_Flufs_Patcher"))
        {
            Debug.LogError("[Rex Carrier Patch] Missing Assets/Blueberrys_Flufs_Patcher");
            if (promptForPath)
            {
                EditorUtility.DisplayDialog(
                    "Export failed",
                    "Missing folder:\nAssets/Blueberrys_Flufs_Patcher",
                    "OK");
            }
            return;
        }

        AssetDatabase.ExportPackage(
            new[] { "Assets/Blueberrys_Flufs_Patcher" },
            path,
            ExportPackageOptions.Recurse);

        try
        {
            string desktop = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
                Path.GetFileName(path));
            File.Copy(path, desktop, true);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Rex Carrier Patch] Desktop copy skipped: " + e.Message);
        }

        Debug.Log("[Rex Carrier Patch] Prod package saved: " + path);
        if (promptForPath)
        {
            EditorUtility.RevealInFinder(path);
            EditorUtility.DisplayDialog("Dev — Export Rexouium Patch", "Saved:\n" + path, "OK");
        }
    }

    static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor FindAvatarDescriptor(GameObject root)
    {
        if (root == null) return null;
        return root.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>()
            ?? root.GetComponentInChildren<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>(true);
    }

    static string DescribeAvatarTarget(GameObject root, VRC.SDK3.Avatars.Components.VRCAvatarDescriptor desc)
    {
        string path = AssetDatabase.GetAssetPath(root);
        bool isPrefabAsset = !string.IsNullOrEmpty(path) && path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase);
        string where = isPrefabAsset ? ("Prefab: " + path) : ("Scene object: " + GetHierarchyPath(root));

        string nameCheck = (root.name + " " + where).ToLowerInvariant();
        bool looksRexouium = nameCheck.Contains("rexouium") || nameCheck.Contains("rex evolved");
        string meshNote = HasBodyMesh(root) ? "Body mesh found." : "No \"Body\" mesh found — double-check this is Rexouium Evolved.";

        if (looksRexouium)
            return "Ready: " + desc.gameObject.name + "\n" + where + "\n" + meshNote;
        return "Selected: " + desc.gameObject.name + "\n" + where + "\n" +
               "Name doesn’t look like Rexouium Evolved — make sure this is the right base.\n" + meshNote;
    }

    static bool HasBodyMesh(GameObject root)
    {
        if (root == null) return false;
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (smr != null && smr.gameObject.name == "Body")
                return true;
        return false;
    }

    static string GetHierarchyPath(GameObject go)
    {
        if (go == null) return "";
        var path = go.name;
        var t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }

    void DrawSocialFooter()
    {
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (iconCommunity != null)
                GUILayout.Label(iconCommunity, GUILayout.Width(22), GUILayout.Height(22));
            GUILayout.Label("Community & Support", EditorStyles.boldLabel);
        }

        var blurb = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { fontSize = 11 };
        GUILayout.Label(
            "Got ideas, bugs, or just want to hang? Jump in — suggestions are always welcome.",
            blurb);

        GUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Discord", "Community chat, help & suggestions"), GUILayout.Height(28)))
                Application.OpenURL(DiscordUrl);
            if (GUILayout.Button(new GUIContent("GitHub", "Projects & updates"), GUILayout.Height(28)))
                Application.OpenURL(GithubUrl);
            if (GUILayout.Button(new GUIContent("Instagram", "Follow blueberry"), GUILayout.Height(28)))
                Application.OpenURL(InstagramUrl);
        }

        GUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "Support · share the avatar, drop feedback on Discord, or just say hi. It helps a lot.",
            MessageType.None);
    }

    static string RunPatch(GameObject avatarRoot)
    {
        var descriptor = FindAvatarDescriptor(avatarRoot);
        if (descriptor == null)
            throw new System.Exception("Select a Rexouium Evolved avatar base (with VRC Avatar Descriptor) first.");

        CreateOrUpdateMawOpenClip();
        CreateOrUpdateMawHoldClip(MawClosedPath, "CarrierMawClosed", open: false);

        ApplySmoothMawOpen(ShowPath);
        ApplyHoldShapes(HidePath, MawClosedShapes, open: false);
        ApplyBellyHold(PoseballUpPath, fill: false);
        EnsureJawOnDownClips(0f);

        var gulp = CreateOrUpdateGulpClip();
        WireGulpIntoVoreFx(gulp);
        WireSmoothCarrierClose();
        ApplyStomachSizeBelly();
        GateStomachSizeUntilGulp();
        RemapBurpMouthOpenToJawOpen();
        MuteStomachChurnLayer();
        FixPoseballDownPaths();
        FixLipSyncOnSelectedAvatar(avatarRoot);
        RenameMoveSeatToGulp();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "Patch v" + Version + " applied to \"" + descriptor.gameObject.name +
               "\" (Rexouium Evolved + Killer Kobold). Re-enter Play Mode / rebuild, then test Show Carrier → Gulp → wait ~0.5s → belly.";
    }

    static void CreateOrUpdateMawOpenClip()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MawOpenPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = "CarrierMawOpen", frameRate = 60f };
            AssetDatabase.CreateAsset(clip, MawOpenPath);
        }

        ApplySmoothOpenCurves(clip);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    static void CreateOrUpdateMawHoldClip(string path, string name, bool open)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = name, frameRate = 60f };
            AssetDatabase.CreateAsset(clip, path);
        }

        if (open)
        {
            ApplySmoothOpenCurves(clip);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }
        else
        {
            foreach (var shape in MawClosedShapes)
                SetHold(clip, shape, 0f);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        EditorUtility.SetDirty(clip);
    }

    static void ApplySmoothMawOpen(string clipPath)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
            throw new FileNotFoundException("Missing clip", clipPath);

        ApplySmoothOpenCurves(clip);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    static void ApplySmoothOpenCurves(AnimationClip clip)
    {
        // Proper ease-in-out open (~0.5s), then hold last frame (clip does not loop).
        const float duration = 0.5f;
        SetBlendshapeCurve(clip, "Jaw_Open", MakeEaseOpenCurve(100f, duration));
        SetBlendshapeCurve(clip, "mouthFunnel", MakeEaseOpenCurve(35f, duration));
        SetBlendshapeCurve(clip, "CheekWidth", MakeEaseOpenCurve(25f, duration));
        SetBlendshapeCurve(clip, "Tongue_Out", MakeEaseOpenCurve(15f, duration));
    }

    static AnimationCurve MakeEaseOpenCurve(float target, float duration)
    {
        // Smooth S-curve from 0 -> target, then a tiny hold so the state sticks open.
        var curve = AnimationCurve.EaseInOut(0f, 0f, duration, target);
        curve.AddKey(duration + 0.05f, target);

        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyBroken(curve, i, false);
        }

        // Flat tangents at the start/end of the ease segment.
        AnimationUtility.SetKeyRightTangentMode(curve, 0, AnimationUtility.TangentMode.Free);
        AnimationUtility.SetKeyLeftTangentMode(curve, 1, AnimationUtility.TangentMode.Free);
        var k0 = curve[0];
        k0.outTangent = 0f;
        curve.MoveKey(0, k0);
        var k1 = curve[1];
        k1.inTangent = 0f;
        curve.MoveKey(1, k1);

        return curve;
    }

    static Keyframe[] SmoothOpenKeys(float target)
    {
        // Kept for compatibility; prefer MakeEaseOpenCurve.
        float duration = 1.0f;
        float[] times = { 0f, 0.2f, 0.45f, 0.7f, 0.9f, 1.0f, 1.05f };
        float[] mults = { 0f, 0.08f, 0.35f, 0.72f, 0.95f, 1f, 1f };
        var keys = new Keyframe[times.Length];
        for (int i = 0; i < times.Length; i++)
            keys[i] = Key(times[i], target * mults[i]);
        return keys;
    }

    static void SetBlendshapeCurve(AnimationClip clip, string shape, AnimationCurve curve)
    {
        var binding = new EditorCurveBinding
        {
            path = BodyPath,
            type = typeof(SkinnedMeshRenderer),
            propertyName = "blendShape." + shape,
        };
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    static void ApplyHoldShapes(string clipPath, IEnumerable<string> shapes, bool open)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
            throw new FileNotFoundException("Missing clip", clipPath);

        var values = new Dictionary<string, float>
        {
            { "Jaw_Open", open ? 100f : 0f },
            { "mouthFunnel", open ? 35f : 0f },
            { "CheekWidth", open ? 25f : 0f },
            { "Tongue_Out", open ? 15f : 0f },
            { "Tongue_Down", 0f },
            { "Cheek_Suck", 0f },
            { "NeckThickness", 0f },
            { "NeckThickness2", 0f },
            { "Weight", 0f },
            { "TorsoThickness", 0f },
            { "BarrelChest", 0f },
        };

        foreach (var shape in shapes)
        {
            float value = values.TryGetValue(shape, out var v) ? v : 0f;
            SetHold(clip, shape, value);
        }

        EditorUtility.SetDirty(clip);
    }

    static void WireSmoothCarrierClose()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(VoreFxPath);
        if (controller == null) return;

        var layerIndex = FindLayerIndex(controller, "Carrier");
        if (layerIndex < 0) return;

        var sm = controller.layers[layerIndex].stateMachine;
        var show = FindState(sm, "Show");
        var hide = FindState(sm, "Hide (DEFAULT)");
        if (show == null || hide == null) return;

        foreach (var t in show.transitions)
        {
            if (t.destinationState == hide)
            {
                t.duration = 0.45f;
                t.hasFixedDuration = true;
            }
        }

        foreach (var t in hide.transitions)
        {
            if (t.destinationState == show)
            {
                // Short blend into the easing open clip (clip itself does the smooth open).
                t.duration = 0.15f;
                t.hasFixedDuration = true;
            }
        }

        EditorUtility.SetDirty(controller);
    }

    static void ApplyStomachSizeBelly()
    {
        // Size 0..6 + Wumbo(7) → body belly via stock Rex shapes.
        // Applied by the Stomach Size layer only while Poseball is on (after Gulp).
        var levels = new (int size, float weight, float torso, float barrel)[]
        {
            (0, 0f, 0f, 0f),
            (1, 15f, 12f, 5f),
            (2, 30f, 25f, 12f),
            (3, 45f, 40f, 18f),
            (4, 60f, 55f, 25f),
            (5, 75f, 70f, 35f),
            (6, 90f, 85f, 45f),
            (7, 100f, 100f, 60f),
        };

        foreach (var level in levels)
        {
            var path = $"Assets/VoreSystem/Animations/CarrierAnims/StomachSizes/StomachSize{level.size}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;

            SetHold(clip, "Weight", level.weight);
            SetHold(clip, "TorsoThickness", level.torso);
            SetHold(clip, "BarrelChest", level.barrel);
            EditorUtility.SetDirty(clip);
        }
    }

    /// <summary>
    /// Stomach Size stays off until gulped, then waits ~0.5s after the gulp before belly grows.
    /// </summary>
    static void GateStomachSizeUntilGulp()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(VoreFxPath);
        if (controller == null) return;

        var layerIndex = FindLayerIndex(controller, "Stomach Size");
        if (layerIndex < 0) return;

        var sm = controller.layers[layerIndex].stateMachine;
        var defaultState = FindState(sm, "(DEFAULT)");
        if (defaultState == null) return;

        var sizeStates = new AnimatorState[8];
        for (int i = 0; i < 8; i++)
        {
            sizeStates[i] = FindState(sm, "StomachSize" + i);
            if (sizeStates[i] == null)
                throw new System.Exception("Missing StomachSize" + i + " state in VoreFX.");
        }

        var delayClip = CreateOrUpdateBellyDelayClip();
        var delay = FindState(sm, "BellyDelay");
        if (delay == null)
            delay = sm.AddState("BellyDelay", new Vector3(230f, 40f, 0f));
        delay.motion = delayClip;
        delay.writeDefaultValues = true;
        delay.speed = 1f;

        // AnyState → Size would skip the delay — remove those.
        foreach (var t in sm.anyStateTransitions.ToArray())
        {
            if (t.destinationState == null) continue;
            if (t.destinationState.name != null && t.destinationState.name.StartsWith("StomachSize"))
                sm.RemoveAnyStateTransition(t);
        }

        // Drop size / delay when not gulped.
        bool hasUngulp = sm.anyStateTransitions.Any(t =>
            t.destinationState == defaultState &&
            t.conditions.Any(c =>
                c.parameter == "Poseball" && c.mode == AnimatorConditionMode.IfNot));
        if (!hasUngulp)
        {
            var t = sm.AddAnyStateTransition(defaultState);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.hasFixedDuration = true;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Poseball");
        }

        // Start delay when gulped (same moment as Gulp anim).
        EnsureBoolTransition(defaultState, delay, "Poseball", whenTrue: true);

        // After gulp + 0.5s, apply current Stomach Size (smooth blend into belly).
        ClearTransitions(delay);
        for (int i = 0; i < 8; i++)
        {
            var t = delay.AddTransition(sizeStates[i]);
            t.hasExitTime = true;
            t.exitTime = 1f; // end of delay clip
            t.duration = 0.5f;
            t.hasFixedDuration = true;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.Equals, i, "StomachSize");
        }
        EnsureBoolTransition(delay, defaultState, "Poseball", whenTrue: false);

        // Size ↔ size immediate (no re-delay). Mute old Exit-on-size-change transitions.
        for (int from = 0; from < 8; from++)
        {
            var state = sizeStates[from];
            foreach (var t in state.transitions.ToArray())
            {
                if (t.isExit)
                    t.mute = true;
                else if (t.destinationState != null &&
                         t.destinationState.name != null &&
                         t.destinationState.name.StartsWith("StomachSize"))
                    state.RemoveTransition(t);
                else if (t.destinationState == defaultState)
                    state.RemoveTransition(t);
            }

            for (int to = 0; to < 8; to++)
            {
                if (from == to) continue;
                var t = state.AddTransition(sizeStates[to]);
                t.hasExitTime = false;
                t.duration = 0.25f;
                t.hasFixedDuration = true;
                t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.Equals, to, "StomachSize");
            }

            EnsureBoolTransition(state, defaultState, "Poseball", whenTrue: false);
        }

        EditorUtility.SetDirty(controller);
    }

    static AnimationClip CreateOrUpdateBellyDelayClip()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BellyDelayPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = "BellyFillDelay", frameRate = 60f };
            AssetDatabase.CreateAsset(clip, BellyDelayPath);
        }

        float length = GulpApproxSeconds + BellyAfterGulpSeconds; // gulp, then +0.5s
        // Hold belly flat the whole wait so size never shows early.
        SetBlendshapeCurve(clip, "Weight", new[] { Key(0f, 0f), Key(length, 0f) });
        SetBlendshapeCurve(clip, "TorsoThickness", new[] { Key(0f, 0f), Key(length, 0f) });
        SetBlendshapeCurve(clip, "BarrelChest", new[] { Key(0f, 0f), Key(length, 0f) });

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void ApplyBellyHold(string clipPath, bool fill)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) return;

        foreach (var kv in BellyFillHold)
            SetHold(clip, kv.Key, fill ? kv.Value : 0f);

        EditorUtility.SetDirty(clip);
    }

    static void ClearShapes(string clipPath, IEnumerable<string> shapes)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) return;
        foreach (var shape in shapes)
            ClearShape(clip, shape);
        EditorUtility.SetDirty(clip);
    }

    static void EnsureJawOnDownClips(float jawValue)
    {
        string[] downClips =
        {
            "Assets/VoreSystem/Animations/CarrierAnims/CarrierPoseballDown.anim",
            "Assets/VoreSystem/Animations/CarrierAnims/CarrierPoseballDownLow.anim",
            "Assets/VoreSystem/Animations/CarrierAnims/CarrierPoseballDownMiddle.anim",
            "Assets/VoreSystem/Animations/CarrierAnims/CarrierPoseballDownHigh.anim",
        };

        foreach (var path in downClips)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;

            // Mouth stays closed while seat is down. Belly fill comes from Stomach Size
            // (gated until Poseball/gulp), not a fixed hold on these clips.
            foreach (var shape in MawClosedShapes)
            {
                if (BellyFillHold.ContainsKey(shape)) continue;
                float value = shape == "Jaw_Open" ? jawValue : 0f;
                SetHold(clip, shape, value);
            }

            ApplyBellyHold(path, fill: false);
            EditorUtility.SetDirty(clip);
        }
    }

    static AnimationClip CreateOrUpdateGulpClip()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(GulpPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = "CarrierGulp", frameRate = 60f };
            AssetDatabase.CreateAsset(clip, GulpPath);
        }

        // Maw closes while a neck bulge travels down, then belly fills.
        SetBlendshapeCurve(clip, "Jaw_Open", new[]
        {
            Key(0.00f, 100f), Key(0.10f, 100f), Key(0.22f, 55f),
            Key(0.35f, 15f), Key(0.50f, 0f), Key(0.85f, 0f),
        });
        SetBlendshapeCurve(clip, "Tongue_Out", new[]
        {
            Key(0.00f, 20f), Key(0.08f, 70f), Key(0.20f, 40f),
            Key(0.35f, 10f), Key(0.50f, 0f), Key(0.85f, 0f),
        });
        SetBlendshapeCurve(clip, "Tongue_Down", new[]
        {
            Key(0.00f, 0f), Key(0.12f, 40f), Key(0.28f, 70f),
            Key(0.45f, 20f), Key(0.60f, 0f), Key(0.85f, 0f),
        });
        SetBlendshapeCurve(clip, "Cheek_Suck", new[]
        {
            Key(0.00f, 0f), Key(0.15f, 20f), Key(0.30f, 55f),
            Key(0.45f, 30f), Key(0.60f, 0f), Key(0.85f, 0f),
        });
        SetBlendshapeCurve(clip, "NeckThickness", new[]
        {
            Key(0.00f, 0f), Key(0.15f, 25f), Key(0.28f, 85f),
            Key(0.42f, 45f), Key(0.58f, 10f), Key(0.75f, 0f), Key(0.85f, 0f),
        });
        SetBlendshapeCurve(clip, "NeckThickness2", new[]
        {
            Key(0.00f, 0f), Key(0.22f, 10f), Key(0.38f, 55f),
            Key(0.52f, 90f), Key(0.65f, 35f), Key(0.78f, 0f), Key(0.85f, 0f),
        });
        SetBlendshapeCurve(clip, "mouthFunnel", new[]
        {
            Key(0.00f, 30f), Key(0.15f, 20f), Key(0.35f, 0f), Key(0.85f, 0f),
        });

        // Belly stays flat during gulp — size fills in ~0.5s after gulp via Stomach Size delay.
        SetBlendshapeCurve(clip, "Weight", new[] { Key(0.00f, 0f), Key(0.85f, 0f) });
        SetBlendshapeCurve(clip, "TorsoThickness", new[] { Key(0.00f, 0f), Key(0.85f, 0f) });
        SetBlendshapeCurve(clip, "BarrelChest", new[] { Key(0.00f, 0f), Key(0.85f, 0f) });

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void WireGulpIntoVoreFx(AnimationClip gulp)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(VoreFxPath);
        if (controller == null)
            throw new FileNotFoundException("Missing VoreFX", VoreFxPath);

        var layerIndex = FindLayerIndex(controller, "Poseball");
        if (layerIndex < 0)
            throw new System.Exception("Poseball layer not found in VoreFX.");

        var sm = controller.layers[layerIndex].stateMachine;
        var up = FindState(sm, "PoseballUp (DEFAULT)");
        var down = FindState(sm, "PoseballDown");
        if (up == null || down == null)
            throw new System.Exception("PoseballUp/PoseballDown states missing.");

        var gulpState = FindState(sm, "Gulp");
        if (gulpState == null)
            gulpState = sm.AddState("Gulp", new Vector3(240f, 125f, 0f));

        gulpState.motion = gulp;
        gulpState.writeDefaultValues = true;
        gulpState.speed = 1f;
        up.writeDefaultValues = false;

        foreach (var t in up.transitions.ToArray())
        {
            bool isPoseballOn = t.conditions.Any(c =>
                c.parameter == "Poseball" && c.mode == AnimatorConditionMode.If);
            if (isPoseballOn && (t.destinationState == down || t.destinationState == gulpState))
                up.RemoveTransition(t);
        }

        var upToGulp = up.AddTransition(gulpState);
        upToGulp.hasExitTime = false;
        upToGulp.duration = 0.05f;
        upToGulp.hasFixedDuration = true;
        upToGulp.AddCondition(AnimatorConditionMode.If, 0f, "Poseball");

        ClearTransitions(gulpState);

        var toDown = gulpState.AddTransition(down);
        toDown.hasExitTime = true;
        toDown.exitTime = 0.95f;
        toDown.duration = 0.05f;
        toDown.hasFixedDuration = true;
        toDown.AddCondition(AnimatorConditionMode.If, 0f, "Poseball");

        var cancelToUp = gulpState.AddTransition(up);
        cancelToUp.hasExitTime = false;
        cancelToUp.duration = 0.05f;
        cancelToUp.hasFixedDuration = true;
        cancelToUp.AddCondition(AnimatorConditionMode.IfNot, 0f, "Poseball");

        EnsureBoolTransition(down, up, "Poseball", false);
        EditorUtility.SetDirty(controller);
    }

    static void RenameMoveSeatToGulp()
    {
        var menu = AssetDatabase.LoadMainAssetAtPath(CarrierMenuPath);
        if (menu == null) return;

        var so = new SerializedObject(menu);
        var controls = so.FindProperty("controls");
        if (controls == null || !controls.isArray) return;

        for (int i = 0; i < controls.arraySize; i++)
        {
            var nameProp = controls.GetArrayElementAtIndex(i).FindPropertyRelative("name");
            if (nameProp == null) continue;
            var n = nameProp.stringValue;
            if (n == "Move Seat" || n == "Move Carrier" || n == "Gulp")
                nameProp.stringValue = "Gulp";
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(menu);
    }

    static void RemapBurpMouthOpenToJawOpen()
    {
        string[] burpClips =
        {
            "Assets/VoreSystem/Animations/SoundToggleAnims/SoundBurp1.anim",
            "Assets/VoreSystem/Animations/SoundToggleAnims/SoundBurp2.anim",
            "Assets/VoreSystem/Animations/SoundToggleAnims/SoundBurp3.anim",
            "Assets/VoreSystem/Animations/SoundToggleAnims/SoundBurpOff.anim",
        };

        foreach (var path in burpClips)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;

            bool changed = false;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.propertyName != "blendShape.MouthOpen") continue;

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                var jawBinding = binding;
                jawBinding.propertyName = "blendShape.Jaw_Open";
                AnimationUtility.SetEditorCurve(clip, jawBinding, curve);
                AnimationUtility.SetEditorCurve(clip, binding, null);
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(clip);
        }
    }

    static void MuteStomachChurnLayer()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(VoreFxPath);
        if (controller == null) return;

        var layers = controller.layers;
        bool changed = false;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name != "Stomach Churn") continue;
            if (Mathf.Approximately(layers[i].defaultWeight, 0f)) continue;
            layers[i].defaultWeight = 0f;
            changed = true;
        }

        if (!changed) return;
        controller.layers = layers;
        EditorUtility.SetDirty(controller);
    }

    static void FixPoseballDownPaths()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/VoreSystem/Animations/CarrierAnims/CarrierPoseballDown.anim");
        if (clip == null) return;

        bool changed = false;
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.path != "BellySeat") continue;
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            var fixedBinding = binding;
            fixedBinding.path = "VoreSystem/Burps/BellySeat";
            AnimationUtility.SetEditorCurve(clip, fixedBinding, curve);
            AnimationUtility.SetEditorCurve(clip, binding, null);
            changed = true;
        }

        // Position/scale bindings use the same path API.
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.path != "BellySeat") continue;
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            var fixedBinding = binding;
            fixedBinding.path = "VoreSystem/Burps/BellySeat";
            AnimationUtility.SetObjectReferenceCurve(clip, fixedBinding, keys);
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            changed = true;
        }

        if (changed)
            EditorUtility.SetDirty(clip);
    }

    static void FixLipSyncOnSelectedAvatar(GameObject avatarRoot)
    {
        var descriptor = FindAvatarDescriptor(avatarRoot);
        if (descriptor == null)
            throw new System.Exception("Selected object has no VRC Avatar Descriptor.");

        // Prefab asset in Project window
        string assetPath = AssetDatabase.GetAssetPath(avatarRoot);
        if (!string.IsNullOrEmpty(assetPath) &&
            assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase) &&
            !PrefabUtility.IsPartOfPrefabInstance(avatarRoot))
        {
            var root = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                bool dirty = false;
                foreach (var d in root.GetComponentsInChildren<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>(true))
                    dirty |= SetMouthOpenBlendshape(d, "Jaw_Open");
                if (dirty)
                    PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return;
        }

        // Scene instance / hierarchy object
        if (SetMouthOpenBlendshape(descriptor, "Jaw_Open"))
        {
            EditorUtility.SetDirty(descriptor);
            var prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(descriptor.gameObject);
            if (prefabRoot != null)
                PrefabUtility.RecordPrefabInstancePropertyModifications(descriptor);
        }
    }

    static bool SetMouthOpenBlendshape(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor descriptor, string shape)
    {
        if (descriptor == null) return false;
        var so = new SerializedObject(descriptor);
        var prop = so.FindProperty("MouthOpenBlendShapeName") ?? so.FindProperty("mouthOpenBlendShapeName");
        if (prop == null) return false;
        if (prop.stringValue == shape) return false;
        prop.stringValue = shape;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(descriptor);
        return true;
    }

    static void EnsureBoolTransition(AnimatorState from, AnimatorState to, string param, bool whenTrue)
    {
        bool found = from.transitions.Any(t =>
            t.destinationState == to &&
            t.conditions.Any(c =>
                c.parameter == param &&
                c.mode == (whenTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot)));
        if (found) return;

        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.hasFixedDuration = true;
        t.AddCondition(whenTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
    }

    static void ClearTransitions(AnimatorState state)
    {
        foreach (var t in state.transitions.ToArray())
            state.RemoveTransition(t);
    }

    static int FindLayerIndex(AnimatorController controller, string name)
    {
        for (int i = 0; i < controller.layers.Length; i++)
            if (controller.layers[i].name == name)
                return i;
        return -1;
    }

    static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        return sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
    }

    static void SetHold(AnimationClip clip, string shape, float value)
    {
        SetBlendshapeCurve(clip, shape, new[] { Key(0f, value), Key(1f / 60f, value) });
    }

    static void ClearShape(AnimationClip clip, string shape)
    {
        var binding = new EditorCurveBinding
        {
            path = BodyPath,
            type = typeof(SkinnedMeshRenderer),
            propertyName = "blendShape." + shape,
        };
        AnimationUtility.SetEditorCurve(clip, binding, null);
    }

    static void SetBlendshapeCurve(AnimationClip clip, string shape, Keyframe[] keys)
    {
        var binding = new EditorCurveBinding
        {
            path = BodyPath,
            type = typeof(SkinnedMeshRenderer),
            propertyName = "blendShape." + shape,
        };
        AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
    }

    static Keyframe Key(float time, float value) => new Keyframe(time, value);
}
