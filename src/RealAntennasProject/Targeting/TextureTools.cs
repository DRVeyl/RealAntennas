using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RealAntennas.Targeting
{
    public class TextureTools
    {
        public static readonly List<VesselType> vesselTypes = new List<VesselType>(16)
        {
            VesselType.Debris,
            VesselType.Probe,
            VesselType.Rover,
            VesselType.Lander,
            VesselType.Ship,
            VesselType.Station,
            VesselType.Base,
            VesselType.Plane,
            VesselType.Relay,
            VesselType.EVA,
            VesselType.Flag,
            VesselType.SpaceObject,
            VesselType.Unknown,
            VesselType.DeployedScienceController,
        };
        public static readonly Dictionary<VesselType, Texture2D> filterTextures = new Dictionary<VesselType, Texture2D>(16);
        public static readonly Dictionary<VesselType, bool> filterStates = new Dictionary<VesselType, bool>(16);
        public static void Initialize()
        {
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();

            if (filterTextures.Count == 0)
                foreach (var t in vesselTypes)
                {
                    string name = GetTextureNameFromVesselType(t);
                    if (sprites.FirstOrDefault(x => x.name == name) is Sprite s)
                        filterTextures[t] = TextureFromSprite(s);
                }

            if (filterStates.Count == 0)
                foreach (var t in filterTextures.Keys)
                    filterStates[t] = true;
        }

        public static void Save(ConfigNode node)
        {
            ConfigNode n = new ConfigNode("FilterSettings");
            foreach (var vType in vesselTypes)
                if (filterStates.TryGetValue(vType, out bool val))
                    n.AddValue($"{vType}", val);
            node.AddNode(n);
        }

        public static void Load(ConfigNode node)
        {
            ConfigNode n = null;
            if (node.TryGetNode("FilterSettings", ref n))
            {
                foreach (var vType in vesselTypes)
                {
                    bool val = false;
                    if (n.TryGetValue($"{vType}", ref val))
                        filterStates[vType] = val;
                }
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

        public static string GetTextureNameFromVesselType(VesselType vesselType)
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
