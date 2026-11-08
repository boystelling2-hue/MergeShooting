using System.IO;
using UnityEditor;
using UnityEngine;

public static class HighResolutionRoleIcons
{
    public static Sprite Load(int role)
    {
        string path="Assets/UI/Workshop/Role"+role+"-HD2.png";
        if(!File.Exists(path))
        {
            const int size=256;var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var p=new Vector2((x+.5f)/size*2-1,(y+.5f)/size*2-1);float d=Shape(role,p);
                float edge=Mathf.Clamp01((.033f-d)*size*.5f+.5f),fill=Mathf.Clamp01(-d*size*.5f+.5f);
                Color c=Color.Lerp(new Color(.10f,.16f,.14f),Color.white,fill);c.a=edge;pixels[y*size+x]=c;
            }
            tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);
        }
        AssetDatabase.ImportAsset(path);var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.filterMode=FilterMode.Bilinear;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.spritePixelsPerUnit=100;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static float Line(Vector2 p,float x,float y,float xx,float yy,float radius)
    {var a=new Vector2(x,y);var d=new Vector2(xx-x,yy-y);return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude))-radius;}
    private static float Box(Vector2 p,Vector2 center,Vector2 half)
    {var q=new Vector2(Mathf.Abs(p.x-center.x),Mathf.Abs(p.y-center.y))-half;return new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0);}
    private static float Polygon(Vector2 p,params Vector2[] v)
    {
        float d=float.MaxValue;bool inside=false;
        for(int i=0,j=v.Length-1;i<v.Length;j=i++) {d=Mathf.Min(d,Line(p,v[j].x,v[j].y,v[i].x,v[i].y,0));if((v[i].y>p.y)!=(v[j].y>p.y)&&p.x<(v[j].x-v[i].x)*(p.y-v[i].y)/(v[j].y-v[i].y)+v[i].x)inside=!inside;}
        return inside?-d:d;
    }
    private static float Shape(int role,Vector2 p)
    {
        if(role==0)return Mathf.Min(Polygon(p,new Vector2(-.10f,-.18f),new Vector2(.10f,-.18f),new Vector2(.10f,.59f),new Vector2(0,.84f),new Vector2(-.10f,.59f)),Mathf.Min(Box(p,new Vector2(0,-.21f),new Vector2(.32f,.055f)),Line(p,0,-.28f,0,-.78f,.07f)));
        if(role==1){Vector2[] arc={new Vector2(-.28f,.75f),new Vector2(.20f,.58f),new Vector2(.45f,.22f),new Vector2(.45f,-.22f),new Vector2(.20f,-.58f),new Vector2(-.28f,-.75f)};float d=Line(p,-.28f,.75f,-.28f,-.75f,.025f);for(int i=1;i<arc.Length;i++)d=Mathf.Min(d,Line(p,arc[i-1].x,arc[i-1].y,arc[i].x,arc[i].y,.045f));return Mathf.Min(d,Mathf.Min(Line(p,-.65f,0,.76f,0,.035f),Mathf.Min(Line(p,.52f,.19f,.76f,0,.035f),Line(p,.52f,-.19f,.76f,0,.035f))));}
        if(role==4)return Mathf.Min(Line(p,0,-.80f,0,.4f,.065f),Polygon(p,new Vector2(0,.88f),new Vector2(.24f,.57f),new Vector2(0,.28f),new Vector2(-.24f,.57f)));
        if(role==3)return Mathf.Min(Box(p,Vector2.zero,new Vector2(.15f,.70f)),Box(p,Vector2.zero,new Vector2(.62f,.15f)));
        return Mathf.Min(Line(p,-.3f,-.80f,-.3f,.80f,.055f),Polygon(p,new Vector2(-.28f,.72f),new Vector2(.58f,.72f),new Vector2(.36f,.36f),new Vector2(.58f,.02f),new Vector2(-.28f,.02f)));
    }
}
