using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RealAntennas.Targeting
{
    public class TextureTools
    {
        public static Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        public static List<string> categories = new List<string>();
        public static void Initialize()
        {
            if (categories.Count == 0)
            {
                categories.Add(GetKeyFromVesselType(VesselType.Debris));
                categories.Add(GetKeyFromVesselType(VesselType.Probe));
                categories.Add(GetKeyFromVesselType(VesselType.Rover));
                categories.Add(GetKeyFromVesselType(VesselType.Lander));
                categories.Add(GetKeyFromVesselType(VesselType.Ship));
                categories.Add(GetKeyFromVesselType(VesselType.Station));
                categories.Add(GetKeyFromVesselType(VesselType.Base));
                categories.Add(GetKeyFromVesselType(VesselType.Plane));
                categories.Add(GetKeyFromVesselType(VesselType.Relay));
                categories.Add(GetKeyFromVesselType(VesselType.EVA));
                categories.Add(GetKeyFromVesselType(VesselType.Flag));
                categories.Add(GetKeyFromVesselType(VesselType.SpaceObject));
                categories.Add(GetKeyFromVesselType(VesselType.Unknown));
                categories.Add(GetKeyFromVesselType(VesselType.DeployedScienceController));
            }
            if (textures.Count == 0)
                foreach (var t in Resources.FindObjectsOfTypeAll<Sprite>().Where(x => categories.Contains(x.name)))
                    textures[t.name] = TextureFromSprite(t);
        }

        public static void Setup(Dictionary<string, bool> filters = null, bool setDefaults = false)
        {
            Initialize();
            if (setDefaults && filters != null)
            {
                foreach (var c in TextureTools.categories)
                    filters.Add(c, false);
                filters[GetKeyFromVesselType(VesselType.Probe)] = true;
                filters[GetKeyFromVesselType(VesselType.Relay)] = true;
                filters[GetKeyFromVesselType(VesselType.Station)] = true;
                filters[GetKeyFromVesselType(VesselType.Ship)] = true;
                filters[GetKeyFromVesselType(VesselType.Lander)] = true;
            }
        }

        // Credit to https://support.unity.com/hc/en-us/articles/206486626-How-can-I-get-pixels-from-unreadable-textures-
        public static Texture2D TextureFromSprite(Sprite sprite)
        {
            var texture = sprite.texture;
            if (sprite.rect.width != sprite.texture.width)
            {
                if (!texture.isReadable)
                {
                    // Create a temporary RenderTexture of the same size as the texture
                    RenderTexture tmp = RenderTexture.GetTemporary(
                                        texture.width,
                                        texture.height,
                                        0,
                                        RenderTextureFormat.Default,
                                        RenderTextureReadWrite.Linear);

                    // Blit the pixels on texture to the RenderTexture
                    Graphics.Blit(texture, tmp);
                    // Backup the currently set RenderTexture
                    RenderTexture previous = RenderTexture.active;
                    // Set the current RenderTexture to the temporary one we created
                    RenderTexture.active = tmp;
                    // Create a new readable Texture2D to copy the pixels to it
                    var readableCopy = new Texture2D(texture.width, texture.height);
                    // Copy the pixels from the RenderTexture to the new Texture
                    readableCopy.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
                    readableCopy.Apply();
                    // Reset the active RenderTexture
                    RenderTexture.active = previous;
                    // Release the temporary RenderTexture
                    RenderTexture.ReleaseTemporary(tmp);
                    texture = readableCopy;
                }

                var sourceRect = sprite.textureRect;
                int x = Mathf.FloorToInt(sourceRect.x);
                int y = Mathf.FloorToInt(sourceRect.y);
                int width = Mathf.FloorToInt(sourceRect.width);
                int height = Mathf.FloorToInt(sourceRect.height);

                Color[] pix = texture.GetPixels(x, y, width, height);
                var destTex = new Texture2D(width, height);
                destTex.SetPixels(pix);
                destTex.Apply();
                return destTex;
            }
            else
                return sprite.texture;
        }

        public static string GetKeyFromVesselType(VesselType vesselType)
        {
             return vesselType switch
            {
                VesselType.Debris => "OrbitIcons_Debris",
                VesselType.SpaceObject => "OrbitIcons_SpaceObj",
                VesselType.Unknown => "OrbitIcons_Unknown",
                VesselType.Probe => "OrbitIcons_Probe",
                VesselType.Relay => "OrbitIcons_CommunicationsRelay",
                VesselType.Rover => "OrbitIcons_Rover",
                VesselType.Lander => "OrbitIcons_Lander",
                VesselType.Ship => "OrbitIcons_Ship",
                VesselType.Plane => "OrbitIcons_Aircraft",
                VesselType.Station => "OrbitIcons_SpaceStation",
                VesselType.Base => "OrbitIcons_Base",
                VesselType.EVA => "OrbitIcons_Kerbal",
                VesselType.Flag => "OrbitIcons_Flag",
                VesselType.DeployedScienceController => "OrbitIcons_DeployedScience",
                VesselType.DeployedSciencePart => "OrbitIcons_DeployedScience",
                _ => "Invalid"
            };
        }
    }
}
