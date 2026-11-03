using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

// Imports the generated atlas and authors persistent scene references. No runtime UI creation.
public static class WorkshopSkinAuthoring
{
    public const string AtlasPath = "Assets/UI/FantasyWorkshop/WorkshopSkin-v1.png";

    [MenuItem("Tools/MergeTD/Apply Workshop Art Skin")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before applying the skin.");
        AssetDatabase.ImportAsset(AtlasPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var existing = provider.GetSpriteRects();
        string[] names = { "Panel", "Action", "Confirm", "Secondary" };
        // Pixel bounds in the original 1254 square atlas; Unity uses a bottom-left origin.
        Rect[] bounds = { new Rect(30,726,570,334), new Rect(654,726,574,334),
            new Rect(30,188,570,338), new Rect(654,188,574,338) };
        var rects = names.Select((name, i) => new SpriteRect {
            name = name, rect = bounds[i], alignment = SpriteAlignment.Center,
            pivot = new Vector2(.5f,.5f), border = new Vector4(100,82,100,82),
            spriteID = existing.FirstOrDefault(r => r.name == name)?.spriteID ?? GUID.Generate()
        }).ToArray();
        provider.SetSpriteRects(rects);
        var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        ids.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name,r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();

        var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToDictionary(s => s.name);
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            string skin = null;
            if (image.name == "Unit and Production Panel" || image.name == "Unit Control Panel" || image.name == "Dialog Frame") skin = "Panel";
            var button = image.GetComponent<Button>();
            if (button != null && !image.name.StartsWith("Cell ") && !image.name.StartsWith("Deck "))
            {
                skin = image.name == "Enter Battle" || image.name == "Start or Pause" ? "Confirm" :
                    image.name == "Produce Unit" || image.name == "Deck Toggle" ? "Action" : "Secondary";
            }
            if (skin == null) continue;
            Undo.RecordObject(image,"Apply workshop art");
            image.sprite = sprites[skin];
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 4;
            image.color = Color.white;
            EditorUtility.SetDirty(image);
            if (button != null)
            {
                Undo.RecordObject(button,"Set art button states");
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.12f,1.12f,1.12f);
                colors.pressedColor = new Color(.72f,.82f,.85f);
                colors.selectedColor = Color.white;
                colors.disabledColor = new Color(.55f,.55f,.55f,.65f);
                button.colors = colors;
                EditorUtility.SetDirty(button);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Workshop art skin saved: four nine-slice sprites and scene bindings.");
    }
}
