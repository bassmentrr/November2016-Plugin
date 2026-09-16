using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace Bassment
{
    [BepInPlugin("xyz.eggstudios.bassment", "Bassment", "0.1.0")]
    public class BassmentPlugin : BaseUnityPlugin
    {
        private const string OldPlayersHost = "https://recroom.azurewebsites.net";
        private const string OldTournamentHost = "http://recroom.azurewebsites.net";

        internal static ConfigEntry<string> RevivalBaseUrl;
        internal static ConfigEntry<bool> LogRedirects;
        internal static ConfigEntry<string> PhotonHost;
        internal static ConfigEntry<int> PhotonPort;
        internal static ConfigEntry<string> PhotonApp;

        internal static Harmony HarmonyInstance;
        internal static bool PhotonPatchApplied;

        private void Awake()
        {
            RevivalBaseUrl = Config.Bind("General", "RevivalBaseUrl", "http://X.X.X.X", "");
            LogRedirects = Config.Bind("General", "LogRedirects", true, "");
            PhotonHost = Config.Bind("Photon", "PhotonHost", "X.X.X.X", "");
            PhotonPort = Config.Bind("Photon", "PhotonPort", 5055, "");
            PhotonApp = Config.Bind("Photon", "PhotonApp", "Master", "");

            Logger.LogInfo("[Bassment] redirecting 2016 API to " + RevivalBaseUrl.Value.TrimEnd('/'));

            Harmony harmony = new Harmony("xyz.eggstudios.bassment");
            HarmonyInstance = harmony;
            harmony.PatchAll(typeof(WWWPatches));
            harmony.PatchAll(typeof(DailyObjectivesPatch));
            harmony.PatchAll(typeof(EmailValidationPatch));
            harmony.PatchAll(typeof(PhotonEnsurePatch));
            harmony.PatchAll(typeof(PhotonInitializePatch));
            Logger.LogInfo("[Bassment] patches applied.");
        }

        internal static void EnsurePhotonPatch()
        {
            if (PhotonPatchApplied)
            {
                return;
            }
            PhotonPatchApplied = true;
            try
            {
                HarmonyInstance.PatchAll(typeof(PhotonConnectPatch));
                Debug.Log("[Bassment] photon patch applied.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Bassment] photon patch failed: " + ex);
            }
        }

        internal static string RewriteUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url;
            }

            string target = RevivalBaseUrl.Value.TrimEnd('/');
            string[] olds = new string[] { OldPlayersHost, OldTournamentHost };

            foreach (string old in olds)
            {
                if (url.StartsWith(old, StringComparison.OrdinalIgnoreCase))
                {
                    string rewritten = target + url.Substring(old.Length);
                    if (LogRedirects.Value)
                    {
                        Debug.Log("[Bassment] " + url + " -> " + rewritten);
                    }
                    return rewritten;
                }
            }

            return url;
        }
    }

    [HarmonyPatch]
    internal static class WWWPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (ConstructorInfo ctor in typeof(WWW).GetConstructors())
            {
                ParameterInfo[] ps = ctor.GetParameters();
                if (ps.Length > 0 && ps[0].ParameterType == typeof(string)) 
                {
                    yield return ctor;
                }
            }
        }

        private static void Prefix(ref string url)
        {
            url = BassmentPlugin.RewriteUrl(url);
        }
    }

    [HarmonyPatch(typeof(UnityExtensions), "IsValidEmail")]
    internal static class EmailValidationPatch
    {
        private static bool Prefix(string email, ref bool __result)
        {
            __result = !string.IsNullOrEmpty(email);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerObjectiveTracker), "GetTodaysObjectives")]
    internal static class DailyObjectivesPatch
    {
        private static FieldInfo dailyField;
        private static FieldInfo descriptionsField;
        private static int[] fallbackTypes = new int[] { 100, 201, 301, 400, 500, 801, 802 };

        private static void Prefix(PlayerObjectiveTracker __instance)
        {
            if (dailyField == null)
            {
                dailyField = AccessTools.Field(typeof(PlayerObjectiveTracker), "dailyObjectives");
                descriptionsField = AccessTools.Field(typeof(PlayerObjectiveTracker), "objectiveTypeDescriptions");
            }
            if (dailyField == null)
            {
                return;
            }
            PlayerObjectiveTracker.Objective[][] days = dailyField.GetValue(null) as PlayerObjectiveTracker.Objective[][];
            if (days != null && days.Length >= 7)
            {
                bool complete = true;
                foreach (PlayerObjectiveTracker.Objective[] day in days)
                {
                    if (day == null || day.Length == 0)
                    {
                        complete = false;
                        break;
                    }
                }
                if (complete)
                {
                    return;
                }
            }
            List<int> pool = BuildPool(__instance);
            PlayerObjectiveTracker.Objective[][] week = new PlayerObjectiveTracker.Objective[7][];
            for (int day = 0; day < 7; day++)
            {
                PlayerObjectiveTracker.Objective[] objectives = new PlayerObjectiveTracker.Objective[3];
                for (int slot = 0; slot < 3; slot++)
                {
                    int pick = pool[(day + slot * 2) % pool.Count];
                    PlayerObjectiveTracker.Objective objective = new PlayerObjectiveTracker.Objective();
                    objective.ObjectiveType = (PlayerObjectiveTracker.ObjectiveType)pick;
                    objective.RequiredScore = slot + 1;
                    objective.Xp = 100;
                    objectives[slot] = objective;
                }
                week[day] = objectives;
            }
            dailyField.SetValue(null, week);
            Debug.Log("[Bassment] daily objectives generated for week of " + DateTime.Today.ToString("yyyy-MM-dd"));
        }

        private static List<int> BuildPool(PlayerObjectiveTracker instance)
        {
            List<int> pool = new List<int>();
            if (descriptionsField != null && instance != null)
            {
                object box = descriptionsField.GetValue(instance);
                PlayerObjectiveTracker.ObjectiveTypeDescription[] descriptions = box as PlayerObjectiveTracker.ObjectiveTypeDescription[];
                if (descriptions != null)
                {
                    foreach (PlayerObjectiveTracker.ObjectiveTypeDescription description in descriptions)
                    {
                        if (description != null)
                        {
                            int type = (int)description.ObjectiveType;
                            if (!pool.Contains(type))
                            {
                                pool.Add(type);
                            }
                        }
                    }
                }
            }
            if (pool.Count < 3)
            {
                foreach (int type in fallbackTypes)
                {
                    if (!pool.Contains(type))
                    {
                        pool.Add(type);
                    }
                }
            }
            return pool;
        }
    }

    [HarmonyPatch(typeof(PhotonNetwork), "ConnectUsingSettings", new Type[] { typeof(string) })]
    internal static class PhotonConnectPatch
    {
        private static string cachedUserId;

        private static void Prefix()
        {
            ServerSettings settings = PhotonNetwork.PhotonServerSettings;
            if (settings == null) 
            {
                return;
            }
            settings.UseMyServer(BassmentPlugin.PhotonHost.Value, BassmentPlugin.PhotonPort.Value, BassmentPlugin.PhotonApp.Value);
            PhotonNetwork.AuthValues = new AuthenticationValues(BuildUserId());
            Debug.Log("[Bassment] photon self-hosted: " + BassmentPlugin.PhotonHost.Value + ":" + BassmentPlugin.PhotonPort.Value);
        }

        private static string BuildUserId()
        {
            if (!string.IsNullOrEmpty(cachedUserId))
            {
                return cachedUserId;
            }
            cachedUserId = ResolveUserId();
            return cachedUserId;
        }

        private static string ResolveUserId()
        {
            try
            {
                CSteamID steamId = SteamUser.GetSteamID();
                if (steamId.IsValid())
                {
                    return steamId.m_SteamID.ToString();
                }
            }
            catch
            {
            }
            try
            {
                if (Player.LocalPlayer != null && Player.LocalPlayer.PlatformId != 0)
                {
                    return Player.LocalPlayer.PlatformId.ToString();
                }
            }
            catch
            {
            }
            return SystemInfo.deviceUniqueIdentifier;
        }
    }

    [HarmonyPatch(typeof(PUNNetworkManager), "InitializeNewConnection")]
    internal static class PhotonEnsurePatch
    {
        private static void Prefix()
        {
            BassmentPlugin.EnsurePhotonPatch();
        }
    }

    [HarmonyPatch(typeof(PUNNetworkManager), "Initialize")]
    internal static class PhotonInitializePatch
    {
        private static void Prefix()
        {
            BassmentPlugin.EnsurePhotonPatch();
        }
    }
}
