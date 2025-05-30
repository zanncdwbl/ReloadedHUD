using System;
using System.Reflection;
using UnityEngine;
using MonoMod.RuntimeDetour;
using System.Linq;
using System.Collections.Generic;

namespace ReloadedHUD
{
    public static class HooksManager
    {
        static RHController hud => RHController.Instance;
        
        public static void Init()
        {
            AddHook(typeof(GameManager), "Pause");
            AddHook(typeof(GameManager), "Unpause");
            AddHook(typeof(PauseMenuController), "ToggleVisibility", "TogglePauseMenuVisibility");
            AddHook(typeof(GameUIRoot), "HideCoreUI");
            AddHook(typeof(GameUIRoot), "ShowCoreUI");
            AddHook(typeof(PlayerController), "Die");
        }

        public static void PostInit()
        {
            CheckAndModifyMod();
        }

        static void Die(Action<PlayerController, Vector2> orig, PlayerController self, Vector2 finalDamageDirection)
        {
            hud.DoFade(0);
            orig(self, finalDamageDirection);
        }

        static void Unpause(Action<GameManager> orig, GameManager self)
        {
            orig(self);
            hud.SwitchEditMode(false);
        }

        static void Pause(Action<GameManager> orig, GameManager self)
        {
            hud.SwitchEditMode(true);
            orig(self);
        }

        static void TogglePauseMenuVisibility(Action<PauseMenuController, bool> orig, PauseMenuController self, bool visible)
        {
            hud.DoPausedFade(visible ? 1 : 0);
            orig(self, visible);
        }

        static void HideCoreUI(Action<GameUIRoot, string> orig, GameUIRoot self, string reason)
        {
            if (!hud.editing)
                hud.DoFade(0);

            orig(self, reason);
        }

        static void ShowCoreUI(Action<GameUIRoot, string> orig, GameUIRoot self, string reason)
        {
            if (!hud.editing)
                hud.DoFade(1);

            orig(self, reason);
        }

        public static Hook AddHook(Type type, string sourceMethodName, string hookMethodName = null)
        {
            if (hookMethodName == null) hookMethodName = sourceMethodName;
            return new Hook(
                type.GetMethod(sourceMethodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
                typeof(HooksManager).GetMethod(hookMethodName, BindingFlags.NonPublic | BindingFlags.Static)
            );
        }

        public static void CheckAndModifyMod()
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var modAssembly = loadedAssemblies.First(a => a.GetName().Name == "ItemTipsMod");

            if (modAssembly == null)
            {
                return;
            }

            try
            {
                var modType = modAssembly.GetType("ItemTipsMod.ItemTipsModule");
                var settingsType = modAssembly.GetType("ItemTipsMod.Settings");

                var modInstance = FindModInstance(modType);
                if (modInstance == null)
                {
                    return;
                }

                FieldInfo settingsField = modType.GetField("_currentSettings", BindingFlags.NonPublic | BindingFlags.Instance);
                var settingsObj = settingsField.GetValue(modInstance);

                FieldInfo leftField = settingsType.GetField("Left", BindingFlags.Public | BindingFlags.Instance); /* 0.01f default*/
                FieldInfo topField = settingsType.GetField("Top", BindingFlags.Public | BindingFlags.Instance); /* 0.20f default */

                MethodInfo getSizeMethodInfo = settingsType.GetMethod("GetSize", BindingFlags.Public | BindingFlags.Instance);
                object sizeValue = getSizeMethodInfo.Invoke(settingsObj, new object[] { 1 });

                var widthFraction = ((Vector2)sizeValue).x / SGUI.SGUIRoot.Main.Size.x;
                leftField.SetValue(settingsObj, 0.98f - widthFraction); /* Give the screen some space on the right side */
                topField.SetValue(settingsObj, 0.30f);
            }
            catch (Exception e)
            {
                MorphUtils.LogError($"Error interacting with ItemTipsMod: {e}");
            }
        }
        static object FindModInstance(Type modType)
        {
            return UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().FirstOrDefault(obj => obj.GetType() == modType);
        }

        public static T GetTypedValue<T>(this FieldInfo This, object instance) { return (T)This.GetValue(instance); }
        public static T ReflectGetField<T>(Type classType, string fieldName, object o = null)
        {
            FieldInfo field = classType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | ((o != null) ? BindingFlags.Instance : BindingFlags.Static));
            return (T)field.GetValue(o);
        }
    }
}
