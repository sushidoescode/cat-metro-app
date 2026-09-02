#if UNITY_ANDROID || UNITY_IOS
using System.Reflection;
using NUnit.Framework;
using Unity.Services.LevelPlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatMetro.Tests.LevelPlay
{
    public sealed class LevelPlayMockPrefabImportTests
    {
        private const string MockPrefabDirectory =
            "Packages/com.unity.services.levelplay/Runtime/Platforms/Editor/EditorAds/Prefabs/";

        [TestCase(MockPrefabDirectory + "MockBannerEditorAd.prefab")]
        [TestCase(MockPrefabDirectory + "MockInterstitialEditorAd.prefab")]
        [TestCase(MockPrefabDirectory + "MockRewardedEditorAd.prefab")]
        public void MockPrefab_ReimportsWithUsableAdSize(string prefabPath)
        {
            Assert.DoesNotThrow(() => AssetDatabase.ImportAsset(
                prefabPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);

            MonoBehaviour mockAdComponent = null;
            FieldInfo mockAdInfoField = null;
            foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                mockAdInfoField = FindField(component.GetType(), "m_MockAdInfo");
                if (mockAdInfoField == null)
                {
                    continue;
                }

                mockAdComponent = component;
                break;
            }

            Assert.That(mockAdComponent, Is.Not.Null,
                $"{prefabPath} has no component inheriting m_MockAdInfo");

            var mockAdInfo = mockAdInfoField.GetValue(mockAdComponent) as LevelPlayAdInfo;
            Assert.That(mockAdInfo, Is.Not.Null,
                $"{prefabPath} did not initialize m_MockAdInfo");
            Assert.That(mockAdInfo.AdSize, Is.Not.Null,
                $"{prefabPath} initialized a mock ad with an invalid adSize payload");
            LogAssert.NoUnexpectedReceived();
        }

        private static FieldInfo FindField(System.Type type, string fieldName)
        {
            while (type != null)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
#endif
