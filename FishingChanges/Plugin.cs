using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using RestlessMods;
using UnityEngine;
using UnityEngine.UI;


namespace FishingChanges;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : RestlessMods.ModBase
{
    private static ConfigEntry<float> _nonRecordFishAnimationTime;
    private static ConfigEntry<bool> _skipFishingMiniGame;

    // Hook here to allow toggling off if desired
    // ReSharper disable once NotAccessedField.Local
    private static ILHook _changedFishingWaitTimesMod;

    private void Awake()
    {
        Setup(typeof(Plugin), PluginInfo.PLUGIN_NAME);
        // Plugin startup logic

        _nonRecordFishAnimationTime = Config.Bind("FishingChanges", "nonRecordAnimationTime", 0f,
            "the game spends 4.5 seconds on showing the fish, this setting changes animationTime for non record fish and trash only");
        _skipFishingMiniGame = Config.Bind("FishingChanges", "skipFishingMiniGame", false,
            "Skips fishing minigame, note if another mod is also patching FishingUI::LateUpdate I will be uninstall that mod.");

        BetterFishingPot.Awake();

        if (_skipFishingMiniGame.Value)
        {
            try
            {
                var moddedMethod =
                    typeof(FishingUI).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
                _harmony.Patch(
                    moddedMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(Plugin), nameof(Plugin.SkipFishingMinigame)))
                );

                // remove other skip minigame mods
                var otherMods = Harmony.GetPatchInfo(moddedMethod);

                // Unpatch all mods on this method 
                var otherPrefixMods = otherMods.Prefixes.Where(patch => patch.owner != _harmony.Id).ToHashSet();
                foreach (var mod in otherPrefixMods)
                {
                    Log.LogInfo(string.Format("removing mod by-{0}", mod.owner));
                    _harmony.Unpatch(moddedMethod, HarmonyPatchType.Prefix, mod.owner);
                }
            }
            catch (Exception _)
            {
                Log.LogError(string.Format("Error around skippingFishingMiniGame Mod: {0}", _.Message));
                // ignored
            }
        }

        var method = getDynamicFinishFishingMethod();
        _changedFishingWaitTimesMod = method == null
            ? null
            : new ILHook(method, ChangeAnimationTime);


        Console.Out.WriteLine($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    private static MethodBase getDynamicFinishFishingMethod()
    {
        var finishFishingMethod = typeof(FishingController)
            ?.GetMethod(nameof(FishingController.FinishFishing));

        var dynamicMethodName = "";

        if (finishFishingMethod == null)
        {
            throw new Exception("Cannot load this mod " + nameof(FishingController.FinishFishing) +
                                " cannot be found.");
        }

        new ILHook(finishFishingMethod, (il) =>
        {
            var cursor = new ILCursor(il);

            // there will be a code of: call .* FishingController::<<<<dynamicMethodName>>>>(

            cursor.TryGotoNext(x =>
            {
                if (!x.OpCode.Equals(OpCodes.Call))
                    return false;

                var opString = x?.ToString();

                var isExpectedMethod =
                        opString.Contains("System.Collections.IEnumerator") &&
                        opString.Contains("FishingController::") &&
                        opString.Contains("(UnityEngine.Vector3,System.Boolean)")
                    ;

                if (!isExpectedMethod) return false;
                dynamicMethodName = opString.Split("::")[1].Split("(")[0];
                Log.LogInfo(finishFishingMethod.Name + " found as " + dynamicMethodName);
                return true;
            });
        });


        return typeof(FishingController)
            .GetMethod(dynamicMethodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetStateMachineTarget();
    }

    // WIP ignore
    private static void NetLogic(ILContext il)
    {
        var cursor = new ILCursor(il);

        if (!cursor.TryGotoNext(x =>
                x.MatchCallvirt(out var reference) && reference.Name.ToLowerInvariant().Contains("add"))) return;

        cursor.TryGotoNext();
        cursor.EmitDelegate(() =>
            {
                var maybeNet = ActionBarInventory.GetPlayer(1).GetSelectedItem();
                if (maybeNet?.GetType().ToString() == "Net")
                {
                    var extra = UnityEngine.Random.Range(0, 10);
                    // 10 fish and 40% to keep adding another
                    for (int i = 0; i < extra || UnityEngine.Random.Range(0, 10) > 4; i++)
                    {
                        var another = (Item)FishingManager.SelectAFish(1, maybeNet as Tool as Rod);
                        if (another != null &&
                            ItemDatabaseAccessor.GetItem(
                                HarmonyLib.Traverse.Create(another).Field("id").GetValue<int>()) is Fish cco)
                        {
                            PlayerInventory.GetPlayer(1).AddItem(new FishInstance(cco), true);
                        }
                    }
                }
            }
        );
    }

    private static void ChangeAnimationTime(ILContext il)
    {
        // Fun fact local variables not field variables will be lost after the enumeration yields
        var cursor = new ILCursor(il);

        for (var i = 0; i < 3; i++)
        {
            if (!cursor.TryGotoNext(x =>
                {
                    if (!x.OpCode.Equals(OpCodes.Ldsfld)) return false;

                    var opString = x?.ToString();
                    var isExpectedMethod =
                            opString.Contains("UnityEngine.WaitForSeconds") &&
                            opString.Contains("CommonReferences::wait1_5")
                        ;

                    return isExpectedMethod;
                })) continue;
            cursor.Remove();

            cursor.EmitDelegate(() => new WaitForSeconds(
                FishingTexts.Get(1).newRecordText.gameObject.activeInHierarchy
                    ? 1.5f
                    : _nonRecordFishAnimationTime.Value / 3f
            ));
        }
    }

    //////////////////////////////////////////////////////////////////
    ///  Instant Catch (by DrStalker)
    static bool SkipFishingMinigame(FishingUI __instance)
    {
        if (!_skipFishingMiniGame.Value) return false;
        if (!__instance.content.activeInHierarchy) return true;

        // Get the private slider object
        var reflectedSlider = Traverse.Create(__instance)
            .Field("progress")
            .GetValue<Slider>();

        if (reflectedSlider != null)
        {
            //Plugin.DebugLog(String.Format("LateUpdatePrefix: reflectedSlider found, value {0}", reflectedSlider.value));
            // Set progress slider value to 1.0 for instant completion
            reflectedSlider.value = 1.0f;
        }

        return true;
    }
}

public class Net : Rod
{
}