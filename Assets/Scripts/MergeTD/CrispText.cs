using UnityEngine;
using UnityEngine.UI;

namespace MergeTD
{
    // Keeps legacy Text bindings/layout while rasterizing glyphs at twice the actual canvas pixel density.
    public sealed class CrispText : Text
    {
        private readonly UIVertex[] quad=new UIVertex[4];
        public const float RasterMultiplier=2;
        protected override void OnPopulateMesh(VertexHelper output)
        {
            output.Clear();if(font==null)return;
            m_DisableFontTextureRebuiltCallback=true;
            try
            {
                var settings=GetGenerationSettings(rectTransform.rect.size);
                settings.scaleFactor*=RasterMultiplier;
                cachedTextGenerator.PopulateWithErrors(text,settings,gameObject);
                var vertices=cachedTextGenerator.verts;float units=1/(pixelsPerUnit*RasterMultiplier);
                if(vertices.Count==0)return;
                Vector2 origin=(Vector2)vertices[0].position*units;Vector2 rounding=PixelAdjustPoint(origin)-origin;
                for(int i=0;i<vertices.Count;i++)
                {
                    int corner=i&3;quad[corner]=vertices[i];quad[corner].position=vertices[i].position*units+(Vector3)rounding;
                    if(corner==3)output.AddUIVertexQuad(quad);
                }
            }
            finally{m_DisableFontTextureRebuiltCallback=false;}
        }
    }
}
