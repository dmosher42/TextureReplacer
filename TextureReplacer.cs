/*
 * Copyright © 2013 Davorin Učakar
 * Copyright © 2013 Ryan Bray
 *
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

// headMesh01
// eyeballLeft, eyeballRight
// pupilLeft, pupilRight
// upTeeth01, upTeeth02, tongue
// headset_band01 (?)
//
[KSPAddon(KSPAddon.Startup.Instantly, true)]
public class TextureReplacer : MonoBehaviour
{
  private static readonly string[] TEXTURE_NAMES = {
    "kerbalHead",
    "kerbalMain", "kerbalMainGrey", "kerbalHelmetGrey",
    "EVAtexture", "EVAhelmet", "EVAjetpack",
    "kerbalMainNRM", "kerbalHelmetNRM", "EVAtextureNRM", "EVAjetpackNRM",
    "GalaxyTex_NegativeX", "GalaxyTex_NegativeY", "GalaxyTex_NegativeZ",
    "GalaxyTex_PositiveX", "GalaxyTex_PositiveY", "GalaxyTex_PositiveZ",
    "suncoronanew",
    "moho00",
    "Eve2_00",
    "evemoon100",
    "KerbinScaledSpace300",
    "NewMunSurfaceMapDiffuse",
    "NewMunSurfaceMap00",
    "Duna5_00",
    "desertplanetmoon00",
    "dwarfplanet100",
    "gas1_clouds",
    "newoceanmoon00",
    "gp1icemoon00",
    "rockymoon100",
    "gp1minormoon100",
    "gp1minormoon200",
    "snowydwarfplanet00"
  };
  private Dictionary<string, Texture2D> mappedTextures = new Dictionary<string, Texture2D>();
  private int memorySaved = 0;
  private int lastTextureCount = 0;
  private int updateCounter = 0;
  private int lastMaterialsLength = 0;
  private Vessel lastVessel = null;
  private bool isInitialised = false;
  private bool isReplaceScheduled = false;

  private void compressTextures()
  {
    List<GameDatabase.TextureInfo> texInfos = GameDatabase.Instance.databaseTexture;
    if (lastTextureCount == texInfos.Count)
      return;

    for (int i = lastTextureCount; i < texInfos.Count; ++i)
    {
      Texture2D texture = texInfos[i].texture;
      TextureFormat format = texture.format;

      if (format == TextureFormat.DXT1 || format == TextureFormat.DXT5)
        continue;

      // `texture.GetPixel() throws an exception if the texture is not readable and hence it cannot
      // be compressed.
      try
      {
        texture.filterMode = FilterMode.Trilinear;
        texture.GetPixel(0, 0);
        texture.Compress(true);
      }
      catch (UnityException)
      {
        continue;
      }

      int nPixels = texture.width * texture.height;
      int oldSize = texture.format == TextureFormat.Alpha8 ? nPixels :
                    texture.format == TextureFormat.RGB24 ? nPixels * 3 : nPixels * 4;
      int newSize = texture.format == TextureFormat.DXT1 ? nPixels / 2 : nPixels;

      int saved = oldSize - newSize;
      memorySaved += saved;

      print(String.Format("[TextureReplacer] Compressed {0} [{1} {2}x{3}], saved {4:0.0} KiB",
                          texture.name, format, texture.width, texture.height, saved / 1024.0));
    }

    lastTextureCount = texInfos.Count;
  }

  private void replaceTextures(Material[] materials)
  {
    print("[TextureReplacer] Replacing textures and setting trilinear filter ...");

    foreach (Material material in materials)
    {
      Texture texture = material.mainTexture;
      if (texture == null || texture.name.Length == 0 || texture.name.StartsWith("Temp"))
        continue;

      if (!mappedTextures.ContainsKey(texture.name))
      {
        // Set trilinear filter. Trilinear filter is also set in initialisation but that loop only
        // iterates through textures in `GameData/`.
        if (texture.filterMode == FilterMode.Bilinear)
          texture.filterMode = FilterMode.Trilinear;

        continue;
      }

      Texture2D newTexture = mappedTextures[texture.name];
      if (newTexture == texture)
        continue;

      // Replace texture. No need to set trilinear filter here as the replacement textures reside in
      // `GameData/` so that has already been set in initialisation.
      material.mainTexture = newTexture;
      Resources.UnloadAsset(texture);

      print("[TextureReplacer] " + texture.name + " replaced");

      Texture normalMap = material.GetTexture("_BumpMap");
      if (normalMap == null || !mappedTextures.ContainsKey(normalMap.name))
        continue;

      Texture2D newNormalMap = mappedTextures[normalMap.name];
      if (newNormalMap == normalMap)
        continue;

      material.SetTexture("_BumpMap", newNormalMap);
      Resources.UnloadAsset(normalMap);

      print("[TextureReplacer] " + normalMap.name + " [normal map] replaced");
    }
  }

  private void initialiseReplacer()
  {
    foreach (string name in TEXTURE_NAMES)
    {
      string url = "TextureReplacer/Textures/" + name;
      Texture2D texture = GameDatabase.Instance.GetTexture(url, false);

      if (texture != null)
      {
        print("[TextureReplacer] Mapping " + name + " -> " + url);
        mappedTextures.Add(name, texture);
      }
    }

    // Replace textures (and apply trilinear filter). This doesn't reach some textures like skybox
    // and kerbalMainGrey. Those will be replaced later.
    replaceTextures((Material[]) Material.FindObjectsOfTypeIncludingAssets(typeof(Material)));
  }

  protected TextureReplacer()
  {
    DontDestroyOnLoad(this);
  }

  protected void Update()
  {
    if (!isInitialised)
    {
      compressTextures();

      if (GameDatabase.Instance.IsReady())
      {
        print(String.Format("[TextureReplacer] Texture compression saved total {0:0.0} MiB = {1:0.0} MB",
                            memorySaved / 1024.0 / 1024.0, memorySaved / 1000.0 / 1000.0));

        initialiseReplacer();
        isInitialised = true;
      }
    }
    else if (HighLogic.LoadedSceneIsFlight)
    {
      // When in flight, perform replacement on each vehicle switch. We have to do this at least
      // because of IVA suits that are reset by KSP on vehicle switch (probably because it sets
      // orange suits to Jeb, Bin & Bob and grey to all others). Replacement is postponed for 1
      // frame to avoid possible race conditions. (I experienced once that IVA textures were not
      // replaced. I suspect race condition as the most plausible cause).
      if (lastVessel != FlightGlobals.ActiveVessel)
      {
        lastVessel = FlightGlobals.ActiveVessel;
        isReplaceScheduled = true;
      }
      else if (isReplaceScheduled)
      {
        isReplaceScheduled = false;
        replaceTextures((Material[]) Resources.FindObjectsOfTypeAll(typeof(Material)));
      }
    }
    else
    {
      lastVessel = null;
      isReplaceScheduled = false;

      if (updateCounter > 0)
      {
        --updateCounter;
      }
      else
      {
        updateCounter = 16;

        // For non-flight scenes we perform replacement once every 10 frames because the next
        // `Resources.FindObjectsOfTypeAll()` call is expensive and the replacement in the
        // initialisation cannot replace certain textures, like skybox for example.
        Material[] materials = (Material[]) Resources.FindObjectsOfTypeAll(typeof(Material));
        if (materials.Length != lastMaterialsLength)
        {
          lastMaterialsLength = materials.Length;
          replaceTextures(materials);
        }
      }
    }
  }
}
