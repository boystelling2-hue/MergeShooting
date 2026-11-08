using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MergeTD;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[InitializeOnLoad]
public static class VisualQualityRepair
{
    private const string Pending="Library/VisualQualityRepair.pending";
    static VisualQualityRepair(){if(File.Exists(Pending))EditorApplication.update+=ApplyPending;}
    private static void ApplyPending()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorApplication.update-=ApplyPending;File.Delete(Pending);
        try{Run();}catch(Exception ex){File.WriteAllText("Library/VisualQualityRepair.txt","FAIL: "+ex);Debug.LogException(ex);}
    }
    [MenuItem("Tools/MergeTD/Repair Visual Quality")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before saving quality changes.");
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
        pipeline.renderScale=1;pipeline.msaaSampleCount=4;EditorUtility.SetDirty(pipeline);
        LandscapeSceneAuthoring.Build();
        var texts=UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        if(texts.Any(t=>!(t is CrispText)))throw new Exception("A scene text still uses low density rendering.");
        var main=UnityEngine.Object.FindFirstObjectByType<MainCraftingScreen>();var so=new SerializedObject(main);var icons=so.FindProperty("roleIcons");
        for(int i=0;i<icons.arraySize;i++){var sprite=(Sprite)icons.GetArrayElementAtIndex(i).objectReferenceValue;if(sprite.rect.width<256)throw new Exception("Low resolution role icon");}
        if(!main.GetComponent<Canvas>().pixelPerfect)throw new Exception("Canvas pixel alignment disabled");
        DefenseBtdVerification.Verify();typeof(DefenseBtdVerification).GetMethod("VerifyArt",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        VerifyMotion();
        AssetDatabase.SaveAssets();
        File.WriteAllText("Library/VisualQualityRepair.txt","PASS: "+texts.Length+" supersampled scene labels, five 256px anti-aliased role icons, native desktop resolution, pixel-aligned canvas, 1.0 URP render scale, 4x MSAA, uncompressed mipmapped terrain/enemies, 1920x1080 target-aware captures; world sprites, BTD HUD/targeting/road checks passed.");
        Debug.Log("Visual quality repair PASS");
    }
    private static void VerifyMotion()=>DefenseBtdVerification.VerifyMotion();
}
