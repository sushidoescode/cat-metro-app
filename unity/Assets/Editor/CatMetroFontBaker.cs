using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Uses the same TMP/FontEngine path as Font Asset Creator. Run explicitly after changing
// a source font; ordinary imports and builds must never repack the shipped static atlases.
public static class CatMetroFontBaker
{
    private const string Output = "Assets/TextMesh Pro/Resources/Fonts & Materials/";

    [MenuItem("Cat Metro/Art/Bake UI Fonts")]
    public static void Bake()
    {
        AssetDatabase.Refresh();
        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            Output + "LiberationSans SDF.asset");
        if (fallback == null) throw new InvalidOperationException("Liberation Sans fallback missing");
        var display = BakeFont("Fredoka", "SemiBold", fallback);
        BakeFont("Nunito", "Regular", fallback);

        var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<TMP_Settings>(
            "Assets/TextMesh Pro/Resources/TMP Settings.asset"));
        settings.FindProperty("m_defaultFontAsset").objectReferenceValue = display;
        var fallbacks = settings.FindProperty("m_fallbackFontAssets");
        fallbacks.arraySize = 1;
        fallbacks.GetArrayElementAtIndex(0).objectReferenceValue = fallback;
        settings.ApplyModifiedPropertiesWithoutUndo();

        string presetPath = Output + "Fredoka-SemiBold BoardTitle.mat";
        var preset = AssetDatabase.LoadAssetAtPath<Material>(presetPath);
        if (preset == null)
        {
            preset = new Material(display.material);
            AssetDatabase.CreateAsset(preset, presetPath);
        }
        else preset.CopyPropertiesFromMaterial(display.material);
        preset.EnableKeyword("UNDERLAY_ON");
        preset.SetColor("_UnderlayColor", new Color(19f / 255f, 28f / 255f, 48f / 255f, 0.3f));
        preset.SetFloat("_UnderlayOffsetY", -0.6f);
        preset.SetFloat("_UnderlaySoftness", 0.25f);
        ShaderUtilities.UpdateShaderRatios(preset);
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
        Debug.Log("CAT_METRO_FONTS baked Fredoka SemiBold + Nunito Regular, 1024x1024 static SDF");
    }

    private static TMP_FontAsset BakeFont(string family, string style, TMP_FontAsset fallback)
    {
        string fontName = family + "-" + style;
        var source = AssetDatabase.LoadAssetAtPath<Font>(
            "Assets/Fonts/" + family + "/" + fontName + ".ttf");
        if (source == null) throw new InvalidOperationException("Missing font " + fontName);
        var baked = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA,
            1024, 1024, AtlasPopulationMode.Dynamic, false);
        var characters = Enumerable.Range(32, 95).Concat(Enumerable.Range(160, 96))
            .Concat("‘’“”–—…€".Select(c => (int)c)).Select(c => (uint)c).ToArray();
        if (!baked.TryAddCharacters(characters, out uint[] missing, true))
            throw new InvalidOperationException(fontName + " missing glyphs: "
                + string.Join(", ", missing.Select(c => "U+" + c.ToString("X4"))));
        baked.atlasPopulationMode = AtlasPopulationMode.Static;
        baked.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
        baked.name = fontName + " SDF";
        baked.atlasTextures[0].name = fontName + " Atlas";
        baked.atlasTextures[0].filterMode = FilterMode.Bilinear;
        baked.material.name = fontName + " SDF Material";

        string path = Output + baked.name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing == null) AssetDatabase.CreateAsset(baked, path);
        else
        {
            // Keep the asset GUID stable for every caller, TMP default, and material preset.
            foreach (var child in AssetDatabase.LoadAllAssetsAtPath(path))
                if (child != existing) UnityEngine.Object.DestroyImmediate(child, true);
            ReplaceFontDefinition(baked, existing);
            baked = existing;
        }
        AssetDatabase.AddObjectToAsset(baked.atlasTextures[0], baked);
        AssetDatabase.AddObjectToAsset(baked.material, baked);
        EditorUtility.SetDirty(baked);

        // Static fonts don't need the TTF at runtime. Ship the OFL notice explicitly too.
        const string licenses = "Assets/Resources/Licenses";
        Directory.CreateDirectory(licenses);
        File.Copy("Assets/Fonts/" + family + "/OFL.txt",
            licenses + "/" + family + "-OFL.txt", true);
        AssetDatabase.ImportAsset(licenses + "/" + family + "-OFL.txt");
        return baked;
    }

    public static void ReplaceFontDefinition(TMP_FontAsset source, TMP_FontAsset destination)
    {
        EditorUtility.CopySerialized(source, destination);
        // TMP keeps nonserialized lookup tables after CopySerialized; refresh already-open
        // labels and the font inspector just as Font Asset Creator does after a rebake.
        destination.ReadFontAssetDefinition();
        TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, destination);
    }
}
