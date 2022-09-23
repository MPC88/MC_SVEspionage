using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace MC_SVEspionage
{
    internal class SVUtil
    {
        internal enum LangTextSection { zero, one, two, three, EffectText }
        internal enum GlobalItemType { none, weapon, equipment, genericitem, ship }
        private const string backupFolder = "Backups/";
        private const string saveTempFilename = "Temp.dat";

        internal static bool opRunning = false;
        internal static string lastBackupPath = "None.  No files have been modifed.";

        internal static string[] GetSavesList()
        {            
            string[] saveFiles = new string[1];

            if (opRunning)
                return null;

            opRunning = true;

            try
            {
                saveFiles = Directory.GetFiles(Application.dataPath + GameData.saveFolderName, "SaveGameData_??.dat");
                if (saveFiles.Length > 0)
                {
                    for (int i = 0; i < saveFiles.Length; i++)
                        saveFiles[i] = Path.GetFileName(saveFiles[i]);
                }
                else
                {
                    saveFiles[0] = "No saves found";
                }
            }
            catch
            {
                saveFiles[0] = "Error loading saves list";
            }

            opRunning = false;
            return saveFiles;
        }

        internal static GameDataInfo LoadGame(string saveFilePath)
        {
            if (opRunning)
                return null;

            opRunning = true;
            
            if (!File.Exists(saveFilePath))
                throw new Exception("Save file does not exist.");

            BinaryFormatter binaryFormatter = new BinaryFormatter();
            FileStream fileStream = File.Open(saveFilePath, FileMode.Open);
            GameDataInfo gameInfo = (GameDataInfo)binaryFormatter.Deserialize(fileStream);
            fileStream.Close();

            if (gameInfo == null)
                throw new Exception("Failed to load save file data.");

            opRunning = false;
            return gameInfo;
        }

        internal static void SaveGame(GameDataInfo gameInfo, string saveFilePath, string modSaveFolder)
        {
            if (opRunning)
                return;

            opRunning = true;

            lastBackupPath = "None.  No files have been modifed.";
            if (File.Exists(saveFilePath))
                lastBackupPath = CreateBackup(saveFilePath, modSaveFolder);

            string tempPath = Path.GetDirectoryName(saveFilePath) + modSaveFolder + saveTempFilename;

            if (!Directory.Exists(Path.GetDirectoryName(tempPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath));

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            BinaryFormatter binaryFormatter = new BinaryFormatter();
            FileStream fileStream = null;
            fileStream = File.Create(tempPath);
            binaryFormatter.Serialize(fileStream, gameInfo);
            fileStream.Close();

            File.Copy(tempPath, saveFilePath, true);
            File.Delete(tempPath);

            opRunning = false;
        }

        private static string CreateBackup(string saveFilePath, string modSaveFolder)
        {
            if (opRunning)
                return null;

            opRunning = true;

            string backupPath = Path.GetDirectoryName(saveFilePath) + modSaveFolder + backupFolder + Path.GetFileNameWithoutExtension(saveFilePath) + "_" + DateTime.Now.ToString("yyyy-MM-dd--HH-mm") + ".dat";
            if (File.Exists(backupPath))
                throw new Exception();

            if (!Directory.Exists(Path.GetDirectoryName(backupPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));

            File.Copy(saveFilePath, backupPath, false);

            opRunning = false;
            return backupPath;
        }

        internal static GameDataInfo RemoveReplaceFromShips(bool isRemoveOp, GameDataInfo gameInfo, int targetID, int newID, GlobalItemType type)
        {
            // Space ship data - player            
            if (gameInfo.spaceShipData != null)
                gameInfo.spaceShipData = RemoveReplaceFromShip(isRemoveOp, gameInfo.spaceShipData, targetID, newID, type);

            // Stored ships
            if (gameInfo.shipLoadouts != null && gameInfo.shipLoadouts.Count > 0)
                for (int i = 0; i < gameInfo.shipLoadouts.Count; i++)
                    if (gameInfo.shipLoadouts[i].data != null)
                        gameInfo.shipLoadouts[i].data = RemoveReplaceFromShip(isRemoveOp, gameInfo.shipLoadouts[i].data, targetID, newID, type);

            // Crew ships
            if (gameInfo.crew != null && gameInfo.crew.Count > 0)
                for (int i = 0; i < gameInfo.crew.Count; i++)
                    if (gameInfo.crew[i].aiChar != null && gameInfo.crew[i].aiChar.shipData != null)
                        gameInfo.crew[i].aiChar.shipData = RemoveReplaceFromShip(isRemoveOp, gameInfo.crew[i].aiChar.shipData, targetID, newID, type);

            // Mercenaries
            if (gameInfo.character != null && gameInfo.character.mercenaries != null && gameInfo.character.mercenaries.Count > 0)
                for (int i = 0; i < gameInfo.character.mercenaries.Count; i++)
                    if (gameInfo.character.mercenaries[i].shipData != null)
                        gameInfo.character.mercenaries[i].shipData = RemoveReplaceFromShip(isRemoveOp, gameInfo.character.mercenaries[i].shipData, targetID, newID, type);

            return gameInfo;
        }

        internal static SpaceShipData RemoveReplaceFromShip(bool isRemoveOp, SpaceShipData ssData, int targetID, int newID, GlobalItemType type)
        {
            if (ssData.cargo != null && ssData.cargo.Count > 0)
                ssData.cargo = RemoveReplaceFromShipCargo(isRemoveOp, ssData.cargo, targetID, newID, type);
            if (type == GlobalItemType.equipment && ssData.equipments != null && ssData.equipments.Count > 0)
                ssData.equipments = SVEquipmentUtil.RemoveReplaceFromShipEquipment(isRemoveOp, ssData.equipments, targetID, newID);

            return ssData;
        }

        internal static List<CargoItem> RemoveReplaceFromShipCargo(bool isRemoveOp, List<CargoItem> cargo, int targetID, int newID, GlobalItemType type)
        {
            if (isRemoveOp)
                cargo.RemoveAll(ci => ci.itemType == (int)type && ci.itemID == targetID);
            else
                cargo.ForEach(ci => { if (ci.itemType == (int)type && ci.itemID == targetID) ci.itemID = newID; });

            return cargo;
        }
        
        internal static List<CargoItem> RemoveReplaceFromShipCargo(bool isRemoveOp, List<CargoItem> cargo, Dictionary<int, int> ids, GlobalItemType type)
        {
            if (isRemoveOp)
                cargo.RemoveAll(ci => ci.itemType == (int)type && ids.ContainsKey(ci.itemID));
            else
                cargo.ForEach((Action<CargoItem>)(ci => { if (ci.itemType == (int)type && ids.ContainsKey(ci.itemID)) ci.itemID = ids[ci.itemID]; }));

            return cargo;
        }

        internal static GameDataInfo RemoveReplaceFromTowedObjects(bool isRemoveOp, GameDataInfo gameInfo, int targetID, int newID, GlobalItemType type)
        {
            if (gameInfo.towedObjects != null && gameInfo.towedObjects.Count > 0)
                if (isRemoveOp)
                    gameInfo.towedObjects.RemoveAll(to => to.driftingObject.itemType == (int)type && to.driftingObject.itemID == targetID);
                else
                    gameInfo.towedObjects.ForEach(to => { if (to.driftingObject.itemType == (int)type && to.driftingObject.itemID == targetID) to.driftingObject.itemID = newID; });

            return gameInfo;
        }

        internal static GameDataInfo RemoveReplaceFromMarkets(bool isRemoveOp, GameDataInfo gameInfo, int targetID, int newID, GlobalItemType type)
        {
            // Stations list
            foreach (Station s in gameInfo.stationList)
                if (s.market != null && s.market.Count > 0)
                    if (isRemoveOp)
                        s.market.RemoveAll(mi => mi.itemType == (int)type && mi.itemID == targetID);
                    else
                        s.market.ForEach(mi => { if (mi.itemType == (int)type && mi.itemID == targetID) mi.itemID = newID; });

            // Arena market
            if (gameInfo.arenaData != null &&
                gameInfo.arenaData.currMarket != null &&
                gameInfo.arenaData.currMarket.Count > 0)
                if (isRemoveOp)
                    gameInfo.arenaData.currMarket.RemoveAll(mi => mi.itemType == (int)type && mi.itemID == targetID);
                else
                    gameInfo.arenaData.currMarket.ForEach(mi => { if (mi.itemType == (int)type && mi.itemID == targetID) mi.itemID = newID; });

            return gameInfo;
        }

        internal static GameDataInfo RemoveReplaceFromSectors(bool isRemoveOp, GameDataInfo gameInfo, int targetID, int newID, GlobalItemType type)
        {
            if (gameInfo.sectors != null && gameInfo.sectors.Count > 0)
                foreach (TSector sec in gameInfo.sectors)
                    if (sec.driftingObjects != null && sec.driftingObjects.Count > 0)
                        if (isRemoveOp)
                            sec.driftingObjects.RemoveAll(dro => dro.itemType == (int)type && dro.itemID == targetID);
                        else
                            sec.driftingObjects.ForEach(dro => { if (dro.itemType == (int)type && dro.itemID == targetID) dro.itemID = newID; });

            if (gameInfo.lastSector != null)
                if (isRemoveOp)
                    gameInfo.lastSector.driftingObjects.RemoveAll(dro => dro.itemType == (int)type && dro.itemID == targetID);
                else
                    gameInfo.lastSector.driftingObjects.ForEach(dro => { if (dro.itemType == (int)type && dro.itemID == targetID) dro.itemID = newID; });

            return gameInfo;
        }
    }

    internal class SVEquipmentUtil
    {
        internal static bool remRepOpFoundEquipment;

        internal static int GetNextID(List<Equipment> equipments)
        {
            int id = -1;
            if (equipments == null || equipments.Count == 0)
                return id;

            foreach (Equipment equipment in equipments)
                if (equipment.id > id)
                    id = equipment.id;
            id++;

            return id;
        }

        internal static int AddToEffectsTextSection(string effectText)
        {
            LanguageTextStruct[] lang = AccessTools.StaticFieldRefAccess<Lang, LanguageTextStruct[]>("section");
            lang[(int)SVUtil.LangTextSection.EffectText].text.Add(effectText);
            return lang[(int)SVUtil.LangTextSection.EffectText].text.Count - 1;
        }

        internal static GameDataInfo AddToRandomStations(GameDataInfo gameDataInfo, Equipment equipment)
        {            
            foreach (Station s in gameDataInfo.stationList)
            {
                if (s.level >= equipment.itemLevel &&
                    UnityEngine.Random.Range(1, 100) <= equipment.sellChance)
                {
                    MarketItem mi = new MarketItem((int)SVUtil.GlobalItemType.equipment, equipment.id, (int)ItemRarity.Common_1, UnityEngine.Random.Range(1, 5), null);
                    s.market.Add(mi);
                }
            }

            return gameDataInfo;
        }

        internal static GameDataInfo RemoveEquipment(GameDataInfo gameDataInfo, int equipmentID)
        {
            SVUtil.opRunning = true;
            remRepOpFoundEquipment = false;

            try
            {
                gameDataInfo = SVUtil.RemoveReplaceFromShips(true, gameDataInfo, equipmentID, 0, SVUtil.GlobalItemType.equipment);
                gameDataInfo = SVUtil.RemoveReplaceFromTowedObjects(true, gameDataInfo, equipmentID, 0, SVUtil.GlobalItemType.equipment);
                gameDataInfo = SVUtil.RemoveReplaceFromMarkets(true, gameDataInfo, equipmentID, 0, SVUtil.GlobalItemType.equipment);
                gameDataInfo = SVUtil.RemoveReplaceFromSectors(true, gameDataInfo, equipmentID, 0, SVUtil.GlobalItemType.equipment);
            }
            catch
            {
                SVUtil.opRunning = false;
                return null;
            }

            SVUtil.opRunning = false;
            return gameDataInfo;
        }

        internal static GameDataInfo ReplaceEquipment(GameDataInfo gameDataInfo, int oldID, int newID)
        {
            SVUtil.opRunning = true;
            remRepOpFoundEquipment = false;

            try
            {
                gameDataInfo = SVUtil.RemoveReplaceFromShips(false, gameDataInfo, oldID, newID, SVUtil.GlobalItemType.equipment);
                gameDataInfo = SVUtil.RemoveReplaceFromTowedObjects(false, gameDataInfo, oldID, newID, SVUtil.GlobalItemType.equipment);
                gameDataInfo = SVUtil.RemoveReplaceFromMarkets(false, gameDataInfo, oldID, newID, SVUtil.GlobalItemType.equipment);
                gameDataInfo = SVUtil.RemoveReplaceFromSectors(false, gameDataInfo, oldID, newID, SVUtil.GlobalItemType.equipment);
            }
            catch
            {
                SVUtil.opRunning = false;
                return null;
            }

            SVUtil.opRunning = false;
            return gameDataInfo;
        }

        internal static List<InstalledEquipment> RemoveReplaceFromShipEquipment(bool isRemoveOp, List<InstalledEquipment> equipments, int targetID, int newID)
        {
            if (isRemoveOp)
                remRepOpFoundEquipment = equipments.RemoveAll(ie => ie.equipmentID == targetID) > 0 || remRepOpFoundEquipment;
            else
                equipments.ForEach(ie => { if (ie.equipmentID == targetID) ie.equipmentID = newID; });

            return equipments;
        }
    }
    internal class SVItemUtil
    {
        internal static int GetNextID(List<Item> items)
        {
            int id = -1;
            if (items == null || items.Count == 0)
                return id;

            foreach (Item item in items)
                if (item.id > id)
                    id = item.id;
            id++;

            return id;
        }

        internal static void ReplaceInDB(Item targetItem, Item newItem)
        {
            List<Item> items = AccessTools.StaticFieldRefAccess<ItemDB, List<Item>>("items");
            if (items != null && items.Count > 0)
                foreach (Item item in items)
                    if (item.id == targetItem.id)
                    {
                        items[items.IndexOf(item)] = newItem;
                        break;
                    }
        }
    }
}
