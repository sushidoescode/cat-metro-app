# Cat Metro patch: LevelPlay 9.5.1 editor mock ads

This local package is an exact copy of Unity Ads Mediation / LevelPlay **9.5.1**, upstream
fingerprint `16215dfb563ea8dbba2e9607e11cdadfd28f9510`, plus one Cat Metro patch.

In `Runtime/Platforms/Editor/EditorAds/Scripts/AdPrefab.cs`, the nested `adSize` object in
`m_AdInfoJson` is encoded as a JSON string. `LevelPlayAdInfo` expects that field to contain a
JSON string; 9.5.1 supplied a dictionary, whose `ToString()` value could not be deserialized.
That left `AdSize` null and printed `LevelPlayAdInfo.GetAdSize` `NullReferenceException`s while
Unity imported each of the three editor mock-ad prefabs.

The patch affects only the mock initializer compiled for
`UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)`. It does not change the runtime parser, suppress
logs, or alter Android/iOS native code.

When upgrading to LevelPlay 9.5.2 or newer, first restore the registry package in an isolated
change and run `CatMetro.Tests.LevelPlay.LevelPlayMockPrefabImportTests`. If all three cases pass
without the import exception, remove this local package fork and its manifest/lock overrides.
