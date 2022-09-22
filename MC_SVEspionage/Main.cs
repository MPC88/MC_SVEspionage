using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace MC_SVTargetScanner
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class Main : BaseUnityPlugin
    {
        // BepInEx Config
        public const string pluginGuid = "mc.starvalor.targetscanner";
        public const string pluginName = "SV Target Scanner";
        public const string pluginVersion = "0.0.2";

        // SV const
        internal const int langEffectTextSection = 4;

        // Uninstaller
        internal const string savePathStr = "/MCSVSaveData/";  // /SaveData/savePathStr/
        internal const string saveDataPrefix = "Intel"; // saveDataPrefix_nn.dat
        internal const string backupPathStr = "UninstBkups/"; //  /SaveData/savePathStr/backupPathStr/
        internal const string tempFilePathStr = "UninstTmp/Temp.dat"; // /SaveData/savePathStr/tempFilePathStr/
        internal static ConfigEntry<string> cfgStatus;
        internal static ConfigEntry<string> cfgSaveFile;
        private static ConfigEntry<bool> cfgRemove;

        // Mod
        internal static ConfigEntry<int> cfgEquipID;
        internal static ConfigEntry<int> cfgItemID;
        internal static PersistentData data;
        internal static bool patchedText = false;

        public void Awake()
        {
            // Target Scanner Config
            cfgEquipID = Config.Bind("Memory",
                "EquipmentID",
                -1,
                "Do not manually edit this value after first run unless you: A. Haven't saved a game or B. Have run the uninstaller on any save game.");
            cfgItemID = Config.Bind("Memory",
                "IntelItemID",
                -1,
                "Do not manually edit this value after first run unless you: A. Haven't saved a game or B. Have run the uninstaller on any save game.");

            UninstallerAwake();
            Harmony.CreateAndPatchAll(typeof(Main));
        }

        private void UninstallerAwake()
        {
            cfgStatus = Config.Bind("Uninstall",
                "1. Status",
                "Idle",
                "Current status of tool.  No input required.");
            cfgStatus.Value = "Idle";
            cfgSaveFile = Config.Bind(new ConfigDefinition("Uninstall", "2 Save Game"),
                "",
                new ConfigDescription("Save game to edit.", new AcceptableValueList<string>(Uninstaller.GetSavesList()), null)
                );
            cfgRemove = Config.Bind("Uninstall",
                "3 Uninstall",
                false,
                "Check this box to start remove process.");
            cfgRemove.Value = false;
        }

        public void Update()
        {
            // Uninstaller
            if ((cfgRemove.Value) && !Uninstaller.running)
            {
                if (GameManager.instance != null && GameManager.instance.inGame)
                    cfgStatus.Value = "Please run uninstall from Main Menu";
                else
                {
                    Uninstaller.RemoveReplaceEquipment(true,
                        cfgEquipID.Value,
                        0,
                        Application.dataPath + GameData.saveFolderName + "/" + cfgSaveFile.GetSerializedValue());
                    Uninstaller.running = false;
                }                
                cfgRemove.Value = false;
            }
        }

        private static void SaveGame()
        {
            if (data == null)
                return;

            string tempPath = Application.dataPath + GameData.saveFolderName + Main.savePathStr + Main.tempFilePathStr;

            if (!Directory.Exists(Path.GetDirectoryName(tempPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath));

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            BinaryFormatter binaryFormatter = new BinaryFormatter();
            FileStream fileStream = null;
            fileStream = File.Create(tempPath);
            binaryFormatter.Serialize(fileStream, data);
            fileStream.Close();

            File.Copy(tempPath, Application.dataPath + GameData.saveFolderName + Main.savePathStr + Main.saveDataPrefix + GameData.gameFileIndex.ToString("00") + ".dat", true);
        }

        private static void LoadGame()
        {
            string associatedData = Application.dataPath + GameData.saveFolderName + Main.savePathStr + Main.saveDataPrefix + GameData.gameFileIndex.ToString("00") + ".dat";
            if (File.Exists(associatedData))
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                FileStream fileStream = File.Open(associatedData, FileMode.Open);
                PersistentData loadData = (PersistentData)binaryFormatter.Deserialize(fileStream);
                fileStream.Close();

                if (loadData == null)
                    data = new PersistentData();
                else
                    data = loadData;
            }
            else
                Main.data = new PersistentData();
        }

        [HarmonyPatch(typeof(EquipmentDB), "LoadDatabase")]
        [HarmonyPostfix]
        private static void EquipmentDB_LoadDatabase_Post(ref List<Equipment> ___equipments)
        {
            ___equipments = MCTargetScanner.EquipmentDBLoadDatabase_Post(___equipments);
        }

        [HarmonyPatch(typeof(ActiveEquipment), "ActivateDeactivate")]
        [HarmonyPrefix]
        private static void ActivateDeactivate_Pre(ActiveEquipment __instance)
        {
            if (__instance.equipment.id == cfgEquipID.Value)
                __instance = MCTargetScanner.ActivateDeactivate_Pre(__instance);
        }

        [HarmonyPatch(typeof(ActiveEquipment), "AddActivatedEquipment")]
        [HarmonyPrefix]
        private static bool ActiveEquipmentAdd_Pre(Equipment equipment, SpaceShip ss, KeyCode key, int rarity, int qnt, ref ActiveEquipment __result)
        {
            if (equipment.id == cfgEquipID.Value)
            {
                __result = MCTargetScanner.ActiveEquipmentAdd_Pre(equipment, ss, key, rarity, qnt);
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(Lang), nameof(Lang.Get), new System.Type[] { typeof(int), typeof(int) })]
        [HarmonyPrefix]
        private static void Lang_Get_Pre()
        {
            if (!patchedText)
            {
                MCTargetScanner.AddToEffectsTextSection();
                Main.patchedText = true;
            }
        }

        [HarmonyPatch(typeof(Lang), "LoadFile")]
        [HarmonyPostfix]
        private static void Lang_LoadFile_Post()
        {
            MCTargetScanner.AddToEffectsTextSection();
        }

        [HarmonyPatch(typeof(GameData), nameof(GameData.SaveGame))]
        [HarmonyPrefix]
        private static void GameDataSaveGame_Pre()
        {
            SaveGame();
        }

        [HarmonyPatch(typeof(GameData), nameof(GameData.LoadGame))]
        [HarmonyPostfix]
        private static void GameDataLoadGame_Post()
        {
            LoadGame();
        }
    }

    [Serializable]
    internal class PersistentData
    {
        internal int scannerEquipID;
        internal List<Item> intelInCargo;

        internal PersistentData()
        {
            intelInCargo = new List<Item>();
        }
    }
}