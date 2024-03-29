﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MC_SVEspionage
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class Main : BaseUnityPlugin
    {
        // BepInEx Config
        public const string pluginGuid = "mc.starvalor.espionage";
        public const string pluginName = "SV Espionage";
        public const string pluginVersion = "0.0.1";

        // Mod
        private const string modSaveFolder = "/MCSVSaveData/";  // /SaveData/ sub folder
        private const string modSaveFilePrefix = "Espionage_"; // modSaveFilePrefixNN.dat
        internal static ConfigEntry<string> cfgStatus;
        internal static ConfigEntry<string> cfgSaveFile;
        private static ConfigEntry<bool> cfgUninstall;
        internal static PersistentData data;
        internal static bool patchedText = false;

        // Debug
        private static BepInEx.Logging.ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("MC_SVEspionage");


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
                    string modDataPath = Application.dataPath + GameData.saveFolderName + modSaveFolder + modSaveFilePrefix + GameData.gameFileIndex.ToString("00") + ".dat";

                    if (!File.Exists(modDataPath))
                    {
                        cfgStatus.Value = "No mod data file assoicated with this save.  No uninstall performed.";
                        cfgUninstall.Value = false;
                        return;
                    }

                    try
                    {   
                        LoadData(selectedSaveIndex);
                        if (data == null)
                            throw new Exception();

                        List<SVUtil.RemoveReplaceEntry> items = new List<SVUtil.RemoveReplaceEntry> {
                            new SVUtil.RemoveReplaceEntry {
                                targetID = data.scannerEquipID,
                                type = SVUtil.GlobalItemType.equipment
                            }
                        };
                        for (int i = 0; i < data.intelInCargo.Count; i++)
                            items.Add(new SVUtil.RemoveReplaceEntry { targetID = data.intelInCargo[i].id, type = SVUtil.GlobalItemType.genericitem });

                        gdi = SVUtil.RemoveObjectsFromGameData(SVUtil.LoadGame(saveFilePath), items);
                    }
                    catch
                    {
                        cfgStatus.Value = "Loading or uninstall operation failed.  No files have been modifed.";
                        cfgUninstall.Value = false;
                    }

                    try
                    {
                        if (gdi != null)
                            SVUtil.SaveGame(gdi, saveFilePath, modSaveFolder);
                    }
                    catch
                    {
                        cfgStatus.Value = "Saving failed.  Backup: " + SVUtil.lastBackupPath;
                        cfgUninstall.Value = false;
                    }

                    try
                    {
                        File.Delete(modDataPath);
                    }
                    catch
                    {
                        cfgStatus.Value = "Uninstalled from save, but failed to delete mod data file.";
                        cfgUninstall.Value = false;
                    }

                    cfgStatus.Value = "Complete.  Save backup: " + SVUtil.lastBackupPath;
                    cfgUninstall.Value = false;
                }                                                
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
            FileStream fileStream = File.Create(tempPath);
            binaryFormatter.Serialize(fileStream, data);
            fileStream.Close();

            File.Copy(tempPath, Application.dataPath + GameData.saveFolderName + modSaveFolder + modSaveFilePrefix + GameData.gameFileIndex.ToString("00") + ".dat", true);
            File.Delete(tempPath);
        }

        private static void LoadData(string saveIndex)
        {
            string modData = Application.dataPath + GameData.saveFolderName + modSaveFolder + modSaveFilePrefix + saveIndex + ".dat";
            try
            {
                if (!saveIndex.IsNullOrWhiteSpace() && File.Exists(modData))
                {
                    BinaryFormatter binaryFormatter = new BinaryFormatter();
                    FileStream fileStream = File.Open(modData, FileMode.Open);
                    PersistentData loadData = (PersistentData)binaryFormatter.Deserialize(fileStream);
                    fileStream.Close();

                    if (loadData == null)
                        data = new PersistentData();
                    else
                        data = loadData;
                }
                else
                    data = new PersistentData();

                List<Equipment> equipments = AccessTools.StaticFieldRefAccess<EquipmentDB, List<Equipment>>("equipments");
                equipments = MCTargetScanner.Load(equipments);

                List<Item> items = AccessTools.StaticFieldRefAccess<ItemDB, List<Item>>("items");
                items = MCIntel.Load(items);
            }
            catch
            {
                SideInfo.AddMsg("<color=red>Espionage mod load failed.</color>");
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
            if (GameManager.instance != null && GameManager.instance.inGame &&
                __instance.equipment.id == data.scannerEquipID)
                    __instance = MCTargetScanner.ActivateDeactivate_Pre(__instance);
        }

        [HarmonyPatch(typeof(ActiveEquipment), "AddActivatedEquipment")]
        [HarmonyPrefix]
        private static bool ActiveEquipmentAdd_Pre(Equipment equipment, SpaceShip ss, KeyCode key, int rarity, int qnt, ref ActiveEquipment __result)
        {
            if (GameManager.instance != null && GameManager.instance.inGame && 
                equipment.id == data.scannerEquipID)
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

        [HarmonyPatch(typeof(MenuControl), nameof(MenuControl.LoadGame))]
        [HarmonyPostfix]
        private static void MenuControlLoadGame_Post()
        {
            LoadData(GameData.gameFileIndex.ToString("00"));
        }

        [HarmonyPatch(typeof(MenuControl), nameof(MenuControl.NewGame))]
        [HarmonyPostfix]
        private static void MenuControlNewGame_Post(NewGameSetup gameSetup)
        {
            if (gameSetup.startQuestCode != 100) // 100 => intro scene to be loaded
                LoadData("");
        }

        [HarmonyPatch(typeof(IntroControl), nameof(IntroControl.StartGame))]
        [HarmonyPostfix]
        private static void IntroControlStartGame_Post()
        {
            LoadData("");
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.SelectItem))]
        [HarmonyPostfix]
        private static void InventorySelectItem_Post(int itemIndex, ref CargoSystem ___cs, ref GameObject ___btnJettison, ref GameObject ___btnScrapItem, ref int ___cargoMode)
        {
            if (itemIndex > 0 && itemIndex < ___cs.cargo.Count &&
                ___cargoMode < 2 && ___cs.cargo[itemIndex].itemID >= MCIntel.startID &&
                ___cs.cargo[itemIndex].itemID <= MCIntel.startID + MCIntel.maxIntels - 1)
            {
                ___btnScrapItem.SetActive(false);
                ___btnJettison.SetActive(false);
            }
        }

        [HarmonyPatch(typeof(CargoSystem), nameof(CargoSystem.RemoveItem))]
        [HarmonyPrefix]
        private static void CargoSystemRemoveItem_Pre(int index, ref List<CargoItem> ___cargo)
        {
            if (___cargo != null && index >= 0 && index < ___cargo.Count && 
                ___cargo[index].itemID >= MCIntel.startID &&
                ___cargo[index].itemID <= MCIntel.startID + MCIntel.maxIntels - 1)
                MCIntel.RemoveIntel(___cargo[index].itemID);
        }
    }

    [Serializable]
    internal class PersistentData
    {
        internal int scannerEquipID;
        internal List<IntelCargo> intelInCargo;

        internal PersistentData()
        {
            scannerEquipID = -1;
            intelInCargo = new List<IntelCargo>();
        }

        internal bool CargoContainsIntelForStation(string stationName)
        {
            if (intelInCargo.Count == 0)
                return false;

            foreach(IntelCargo intel in intelInCargo)
                if(intel.stationName.Equals(stationName))
                    return true;

            return false;
        }

        [Serializable]
        internal class IntelCargo
        {
            internal int id;
            internal string stationName;

            public IntelCargo()
            {
                id = -1;
                stationName = "";
            }
        }
    }
}