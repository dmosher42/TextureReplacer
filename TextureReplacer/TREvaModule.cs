﻿/*
 * Copyright © 2014 Davorin Učakar
 *
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using UnityEngine;

namespace TextureReplacer
{
  public class TREvaModule : PartModule
  {
    Reflections.Script reflectionScript = null;

    [KSPField(isPersistant = true)]
    bool isInitialised = false;

    [KSPField(isPersistant = true)]
    public bool hasEvaSuit = false;

    [KSPEvent(guiActive = true, guiName = "Toggle EVA Suit", active = true)]
    public void toggleEvaSuit()
    {
      Personaliser personaliser = Personaliser.instance;

      if (personaliser.personalise(part, !hasEvaSuit))
      {
        hasEvaSuit = !hasEvaSuit;

        if (hasEvaSuit && reflectionScript != null)
          reflectionScript.update(true);
      }
      else
      {
        hasEvaSuit = true;
        ScreenMessages.PostScreenMessage("No breathable atmosphere", 5.0f,
                                         ScreenMessageStyle.UPPER_CENTER);
      }
    }

    public override void OnStart(StartState state)
    {
      Personaliser personaliser = Personaliser.instance;

      if (reflectionScript == null && Reflections.instance.reflectionType == Reflections.Type.REAL)
      {
        reflectionScript = new Reflections.Script(part);

        foreach (SkinnedMeshRenderer smr in part.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
          if (smr.name == "visor")
          {
            reflectionScript.applyVisor(smr.material);
            break;
          }
        }
      }

      if (!isInitialised)
      {
        hasEvaSuit = !personaliser.isAtmSuitEnabled;
        isInitialised = true;
      }

      if (!personaliser.personalise(part, hasEvaSuit))
        hasEvaSuit = true;
    }

    public override void OnUpdate()
    {
      Personaliser personaliser = Personaliser.instance;

      if (hasEvaSuit)
      {
        if (reflectionScript != null)
          reflectionScript.update();
      }
      else if (!personaliser.isAtmBreathable())
      {
        personaliser.personalise(part, true);
        hasEvaSuit = true;

        if (reflectionScript != null)
          reflectionScript.update(true);
      }
    }

    public void OnDestroy()
    {
      if (reflectionScript != null)
        reflectionScript.destroy();
    }
  }
}
