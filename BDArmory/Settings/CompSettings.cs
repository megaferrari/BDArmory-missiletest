using System.Linq;
using System.Reflection;
using UnityEngine;

using System.IO;
using System.Collections.Generic;
using System;
using BDArmory.UI;

namespace BDArmory.Settings
{
    public class CompSettings
    {
        // Settings overrides for AI/WM settings for competition rules compliance

        static readonly string CompSettingsPath = Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath, "GameData/BDArmory/PluginData/Comp_settings.cfg"));
        static public bool CompOverridesEnabled = false;
        static public bool CompVesselChecksEnabled = false;
        static public bool CompPriceChecksEnabled = false;
        static public bool CompBanChecksEnabled = false;
        public static readonly Dictionary<string, float> CompOverrides = new()
        {
            // FIXME there's probably a few more things that could get set here for AI/WM override if needed in specific rounds.
            //AI Min/max Alt?
            //AI postStallAoA?
            //AI allowRamming?
            //WM gunRange?
            //WM multiMissileTgtNum
            {"extensionCutoffTime", -1},
            {"extendDistanceAirToAir", -1},
            {"MONOCOCKPIT_VIEWRANGE", -1},
            {"DUALCOCKPIT_VIEWRANGE", -1},
            {"guardAngle", -1},
            {"collisionAvoidanceThreshold", -1},
            {"vesselCollisionAvoidanceLookAheadPeriod", -1},
            {"vesselCollisionAvoidanceStrength", -1 },
            {"idleSpeed", -1},
            {"DISABLE_SAS", 0}, //0/1 for F/T
		};
        public static readonly Dictionary<string, float> vesselChecks = new()
        {
            //{"maxStacking", -1}, //wing Stacking %. No limit if -1
            //{"maxPartCount", -1}, //part count. no limit if -1
            //{"maxLtW", -1},        //Lift-to-Weight ratio. -1 for no limit   
            //{"maxTWR", -1},        //Thrust-Weight ratio. -1 for no limit   
            //{"maxEngines", -1},
            //{"maxMass", -1},
            //{"pointBuyBudget", -1}, //for comps with point buy systems for limiting armament/etc. if enabled, will check parts against partPointCosts
        };
        public static readonly Dictionary<string, float> partPointCosts = new()
        {
            //{"bahaBrowningAnm2", 1},
        };
        public static readonly Dictionary<string, float> partBlacklist = new()
        {
            //{"bahaCloakingDevice", 2}, //flag craft containing more than allowed number of X
        };
        /// <summary>
        /// Load P:S AI/Wm override settings from file.
        /// </summary>
        public static void Load()
        {
            CompOverridesEnabled = false;
            CompVesselChecksEnabled = false;
            CompPriceChecksEnabled = false;
            CompBanChecksEnabled = false;
            if (!File.Exists(CompSettingsPath))
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.CompSettings]: Override settings not present, skipping.");
                return;
            }
            ConfigNode fileNode = ConfigNode.Load(CompSettingsPath);
            if (fileNode.HasNode("AIWMChecks"))
            {
                CompOverridesEnabled = true;
                ConfigNode settings = fileNode.GetNode("AIWMChecks");

                foreach (ConfigNode.Value fieldNode in settings.values)
                {
                    if (float.TryParse(fieldNode.value, out float fieldValue))
                        CompOverrides[fieldNode.name] = fieldValue; // Add or set the override.
                }
                if (BDArmorySettings.DEBUG_OTHER)
                {
                    Debug.Log($"[BDArmory.CompSettings]: Comp AI/WM overrides loaded");
                    foreach (KeyValuePair<string, float> entry in CompOverrides)
                    {
                        Debug.Log($"[BDArmory.CompSettings]: {entry.Key}, value {entry.Value} added");
                    }
                }
            }
            if (fileNode.HasNode("VesselChecks"))
            {
                CompVesselChecksEnabled = true;
                ConfigNode settings = fileNode.GetNode("VesselChecks");

                foreach (ConfigNode.Value fieldNode in settings.values)
                {
                    if (float.TryParse(fieldNode.value, out float fieldValue))
                        vesselChecks[fieldNode.name] = fieldValue; // Add or set the override.
                }
                if (BDArmorySettings.DEBUG_OTHER)
                {
                    Debug.Log($"[BDArmory.CompSettings]: Comp vessel checks loaded");
                    foreach (KeyValuePair<string, float> entry in vesselChecks)
                    {
                        Debug.Log($"[BDArmory.CompSettings]: {entry.Key}, value {entry.Value} added");
                    }
                }
            }
            if (fileNode.HasNode("PartCosts"))
            {
                CompPriceChecksEnabled = true;
                ConfigNode settings = fileNode.GetNode("PartCosts");

                foreach (ConfigNode.Value fieldNode in settings.values)
                {
                    if (float.TryParse(fieldNode.value, out float fieldValue))
                        partPointCosts[fieldNode.name] = fieldValue; // Add or set the override.
                }
                if (BDArmorySettings.DEBUG_OTHER)
                {
                    Debug.Log($"[BDArmory.CompSettings]: Comp part costs loaded");
                    foreach (KeyValuePair<string, float> entry in partPointCosts)
                    {
                        Debug.Log($"[BDArmory.CompSettings]: {entry.Key}, value {entry.Value} added");
                    }
                }
            }
            if (fileNode.HasNode("PartBlackList"))
            {
                CompBanChecksEnabled = true;
                ConfigNode settings = fileNode.GetNode("PartBlackList");

                foreach (ConfigNode.Value fieldNode in settings.values)
                {
                    if (float.TryParse(fieldNode.value, out float fieldValue))
                        partBlacklist[fieldNode.name] = fieldValue;
                }
                if (BDArmorySettings.DEBUG_OTHER)
                {
                    Debug.Log($"[BDArmory.CompSettings]: Comp part blacklist loaded");
                    foreach (KeyValuePair<string, float> entry in partBlacklist)
                    {
                        Debug.Log($"[BDArmory.CompSettings]: {entry.Key}, value {entry.Value} added");
                    }
                }
            }
        }
    }
}