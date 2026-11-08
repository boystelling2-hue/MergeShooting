using System.IO;
using MergeTD;
using UnityEditor;
using UnityEngine;

public static class DefenseArtAssets
{
    public const string DirectoryPath="Assets/Art/DefenseMeadow/";
    public static Sprite Import(string name)
    {
        string path=DirectoryPath+name+".png";
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        bool enemy=name=="AmberSlime-v1"||name=="CyanRunner-v1"||name=="AmethystGolem-v1";
        importer.spritePixelsPerUnit=100;importer.maxTextureSize=enemy?512:2048;importer.mipmapEnabled=true;importer.mipMapsPreserveCoverage=false;
        importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Trilinear;
        importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    // A separate transparent road layer follows the simulation route exactly. The painted terrain is never modified.
    public static Sprite Road(DefenseBattle battle)
    {
        const int width=1080,height=660;const float tile=60;
        var points=new Vector2[battle.Route.Length];for(int i=0;i<points.Length;i++)points[i]=new Vector2((battle.Route[i].x+.5f)*tile,height-(battle.Route[i].y+.5f)*tile);
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);var colors=new Color[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            Vector2 p=new Vector2(x+.5f,y+.5f);float distance=float.MaxValue;
            for(int i=1;i<points.Length;i++){var line=points[i]-points[i-1];float t=Mathf.Clamp01(Vector2.Dot(p-points[i-1],line)/line.sqrMagnitude);distance=Mathf.Min(distance,Vector2.Distance(p,points[i-1]+line*t));}
            float noise=Mathf.PerlinNoise(x*.045f,y*.045f);float edge=distance+(noise-.5f)*1.2f;
            Color c=Color.clear;
            if(edge<30)c=new Color(.30f,.34f,.16f,.35f*(1-Mathf.Clamp01((edge-28)/2)));
            if(edge<27.5f)c=new Color(.57f,.43f,.23f);
            if(edge<24.5f){float n=(noise-.5f)*.08f;c=new Color(.86f+n,.74f+n,.49f+n);}
            // A restrained, broad pebble texture; no cell seams or baked UI.
            if(edge<21&&Mathf.PerlinNoise(x*.21f+71,y*.21f+19)>.77f)c=new Color(.72f,.61f,.40f);
            colors[y*width+x]=c;
        }
        texture.SetPixels(colors);texture.Apply();string path=DirectoryPath+"MeadowRoute-v1.png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);return Import("MeadowRoute-v1");
    }
}
