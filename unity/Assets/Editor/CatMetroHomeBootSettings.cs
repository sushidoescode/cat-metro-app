using System.Reflection;
using UnityEditor;
using UnityEngine;
using CatMetro.Presentation.Theme;

namespace CatMetro.Editor
{
    public static class CatMetroHomeBootSettings
    {
        [MenuItem("Cat Metro/Art/Apply Home Boot Settings")]
        public static void Apply()
        {
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = Palette.InkNavy;
            var logo = Resources.Load<Sprite>("Theme/HomeCatMark");
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, logo) };
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Store/Icons/cat-metro-icon-foreground-512.png");
            // Unity 6000.3 only exposes an Android texture getter; use the same serialized
            // PlayerSettings object as its inspector to author this one native field.
            var settings = (SerializedObject)typeof(PlayerSettings).GetMethod("GetSerializedObject",
                BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            settings.Update();
            settings.FindProperty("androidSplashScreen").objectReferenceValue = icon;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("HOME_BOOT settings applied: InkNavy, cat icon, Unity splash disabled.");
        }
    }
}
