using System.IO;
using UnityEditor;
using UnityEngine;

public static class DefenseVisualAssets
{
    public static Sprite Sprite(string name)
    {
        const string directory="Assets/UI/Defense";
        string path=directory+"/"+name+".png";
        if(!File.Exists(path))
        {
            Directory.CreateDirectory(directory);int size=name=="RangeThin"?256:64;
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float nx=(x+.5f)/size*2-1,ny=(y+.5f)/size*2-1,r=Mathf.Sqrt(nx*nx+ny*ny);Color c=Color.clear;
                if(name=="RangeThin") {if(r<.975f)c=new Color(1,1,1,r>.962f?.85f:.035f);}
                else if(name=="Orb"){if(r<.85f)c=Color.white;}
                else if(name=="Heart"){float a=nx*nx+ny*ny-.6f;if(a*a*a-nx*nx*ny*ny*ny<0)c=Color.white;}
                else if(name=="Coin"){if(r<.85f)c=r>.65f?new Color(.65f,.53f,.3f):Color.white;if(Mathf.Abs(nx)<.07f&&Mathf.Abs(ny)<.43f)c=new Color(.6f,.46f,.2f);}
                else
                {
                    float body=nx*nx/.77f+ny*ny/.70f;
                    if(body<1)c=body>.80f?new Color(.23f,.18f,.12f):ny>.25f?Color.white:new Color(.83f,.88f,.86f);
                    if(body<.8f&&ny>-.12f&&ny<.18f&&(Mathf.Abs(nx-.25f)<.09f||Mathf.Abs(nx+.25f)<.09f))c=new Color(.08f,.10f,.09f);
                }
                pixels[y*size+x]=c;
            }
            tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=100;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
