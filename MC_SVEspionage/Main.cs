using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace MC_SVEspionage
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class Main : BaseUnityPlugin
    {
        // BepInEx Config
        public const string pluginGuid = "mc.starvalor.targetscanner";
        public const string pluginName = "SV Target Scanner";
        public const string pluginVersion = "0.0.2";

        // Mod
        private const string modSaveFolder = "/MCSVSaveData/";  // /SaveData/ sub folder
        private const string modSaveFilePrefix = "Espionage_"; // modSaveFilePrefixNN.dat
        internal static ConfigEntry<string> cfgStatus;
        internal static ConfigEntry<string> cfgSaveFile;
        private static ConfigEntry<bool> cfgUninstall;
        internal static PersistentData data;
        internal static bool patchedText = false;

        public void Awake()
        {
            UninstallerAwake();
            Harmony.CreateAndPatchAll(typeof(Main));
        }

        private void UninstallerAwake()
        {
            cfgStatus = Config.Bind("Uninstall",
                "1. Status",
                "",
                "Current status of tool.  No input required.");
            cfgStatus.Value = "Idle";
            cfgSaveFile = Config.Bind(new ConfigDefinition("Uninstall", "2 Save Game"),
                "",
                new ConfigDescription("Save game to edit.", new AcceptableValueList<string>(SVUtil.GetSavesList()), null));
            cfgUninstall = Config.Bind("Uninstall",
                "3 Uninstall",
                false,
                "Check this box to start remove process.");
            cfgUninstall.Value = false;
        }

        public void Update()
        {
            // Uninstaller
            if ((cfgUninstall.Value) && !SVUtil.opRunning)
            {
                if (GameManager.instance != null && GameManager.instance.inGame)
                    cfgStatus.Value = "Please run uninstall from Main Menu";
                else
                {
                    GameDataInfo gdi = null;
                    string selectedSaveIndex = cfgSaveFile.GetSerializedValue();
                    selectedSaveIndex = selectedSaveIndex.Remove(selectedSaveIndex.IndexOf('.')).Remove(0, selectedSaveIndex.IndexOf('_') + 1);
                    string saveFilePath = Application.dataPath + GameData.saveFolderName + "/" + cfgSaveFile.GetSerializedValue();
                    try
                    {
                        LoadData(selectedSaveIndex);
                        gdi = SVEquipmentUtil.RemoveEquipment(SVUtil.LoadGame(saveFilePath), data.scannerEquipID);
                    }
                    catch
                    {
                        cfgStatus.Value = "Loading or uninstall operation failed.  No files have been modifed.";
                    }

                    try
                    {
                        if (gdi != null)
                            SVUtil.SaveGame(gdi, saveFilePath, modSaveFolder);
                    }
                    catch
                    {
                        cfgStatus.Value = "Saving failed.  Backup: " + SVUtil.lastBackupPath;
                    }

                    cfgStatus.Value = "Complete.  Save backup: " + SVUtil.lastBackupPath;
                }                                
                cfgUninstall.Value = false;
            }
        }

        private static void SaveGame()
        {
            if (data == null)
                return;

            string tempPath = Application.dataPath + GameData.saveFolderName + modSaveFolder + "EspTemp.dat";

            if (!Directory.Exists(Path.GetDirectoryName(tempPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath));

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            BinaryFormatter binaryFormatter = new BinaryFormatter();
            FileStream fileStream = null;
            fileStream = File.Create(tempPath);
            binaryFormatter.Serialize(fileStream, data);
            fileStream.Close();

            File.Copy(tempPath, Application.dataPath + GameData.saveFolderName + modSaveFolder + modSaveFilePrefix + GameData.gameFileIndex.ToString("00") + ".dat", true);
        }

        private static void LoadData(string saveIndex)
        {
            string associatedData = Application.dataPath + GameData.saveFolderName + modSaveFolder + modSaveFilePrefix + saveIndex + ".dat";
            try
            {
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
                    data = new PersistentData();
            }
            catch
            {
                data = null;
            }
        }

        private static void DeleteData(string saveIndex)
        {
            string associatedData = Application.dataPath + GameData.saveFolderName + modSaveFolder + modSaveFilePrefix + saveIndex + ".dat";
            if (File.Exists(associatedData))
                File.Delete(associatedData);
        }

        [HarmonyPatch(typeof(ActiveEquipment), "ActivateDeactivate")]
        [HarmonyPrefix]
        private static void ActivateDeactivate_Pre(ActiveEquipment __instance)
        {
            if (__instance.equipment.id == data.scannerEquipID)
                __instance = MCTargetScanner.ActivateDeactivate_Pre(__instance);
        }

        [HarmonyPatch(typeof(ActiveEquipment), "AddActivatedEquipment")]
        [HarmonyPrefix]
        private static bool ActiveEquipmentAdd_Pre(Equipment equipment, SpaceShip ss, KeyCode key, int rarity, int qnt, ref ActiveEquipment __result)
        {
            if (equipment.id == data.scannerEquipID)
            {
                __result = MCTargetScanner.ActiveEquipmentAdd_Pre(equipment, ss, key, rarity, qnt);
                return false;
            }

            return true;
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
            LoadData(GameData.gameFileIndex.ToString("00"));
            if (data == null)
            {
                SideInfo.AddMsg("<color=red>Espionage mod load failed.</color>");
                return;
            }

            List<Equipment> equipments = AccessTools.StaticFieldRefAccess<EquipmentDB, List<Equipment>>("equipments");
            equipments = MCTargetScanner.Load(equipments);

            List<Item> items = AccessTools.StaticFieldRefAccess<ItemDB, List<Item>>("items");
            items = MCIntel.Load(items);
        }
    }

    [Serializable]
    internal class PersistentData
    {
        internal int scannerEquipID;
        internal List<Item> intelInCargo;

        internal PersistentData()
        {
            scannerEquipID = -1;
            intelInCargo = new List<Item>();
        }
    }
}