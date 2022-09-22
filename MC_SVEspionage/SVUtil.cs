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
    }

    internal class SVEquipmentUtil
    {
        internal static bool remRepOpFoundEquipment;

        internal static GameDataInfo RemoveEquipment(GameDataInfo gameDataInfo, int equipmentID)
        {
            SVUtil.opRunning = true;
            remRepOpFoundEquipment = false;

            try
            {
                gameDataInfo = RemoveReplaceFromShips(true, gameDataInfo, equipmentID, 0);
                gameDataInfo = RemoveReplaceFromTowedObjects(true, gameDataInfo, equipmentID, 0);
                gameDataInfo = RemoveReplaceFromMarkets(true, gameDataInfo, equipmentID, 0);
                gameDataInfo = RemoveReplaceFromSectors(true, gameDataInfo, equipmentID, 0);
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
                gameDataInfo = RemoveReplaceFromShips(false, gameDataInfo, oldID, newID);
                gameDataInfo = RemoveReplaceFromTowedObjects(false, gameDataInfo, oldID, newID);
                gameDataInfo = RemoveReplaceFromMarkets(false, gameDataInfo, oldID, newID);
                gameDataInfo = RemoveReplaceFromSectors(false, gameDataInfo, oldID, newID);
            }
            catch
            {
                SVUtil.opRunning = false;
                return null;
            }

            SVUtil.opRunning = false;
            return gameDataInfo;
        }

        private static GameDataInfo RemoveReplaceFromShips(bool isRemoveOp, GameDataInfo gameInfo, int targetIDVal, int newIDVal)
        {
            // Space ship data - player            
            if (gameInfo.spaceShipData != null)
            {
                if (gameInfo.spaceShipData.cargo != null && gameInfo.spaceShipData.cargo.Count > 0)
                    gameInfo.spaceShipData.cargo = RemoveReplaceFromShipCargo(isRemoveOp, gameInfo.spaceShipData.cargo, targetIDVal, newIDVal);
                if (gameInfo.spaceShipData.equipments != null && gameInfo.spaceShipData.equipments.Count > 0)
                    gameInfo.spaceShipData.equipments = RemoveReplaceFromShipEquipment(isRemoveOp, gameInfo.spaceShipData.equipments, targetIDVal, newIDVal);
            }
            else // No player = no bueno                
                throw new Exception();

            // Stored ships
            if (gameInfo.shipLoadouts != null && gameInfo.shipLoadouts.Count > 0)
                for (int i = 0; i < gameInfo.shipLoadouts.Count; i++)
                    if (gameInfo.shipLoadouts[i].data != null)
                    {
                        if (gameInfo.shipLoadouts[i].data.cargo != null && gameInfo.shipLoadouts[i].data.cargo.Count > 0)
                            gameInfo.shipLoadouts[i].data.cargo = RemoveReplaceFromShipCargo(isRemoveOp, gameInfo.shipLoadouts[i].data.cargo, targetIDVal, newIDVal);
                        if (gameInfo.shipLoadouts[i].data.equipments != null && gameInfo.shipLoadouts[i].data.equipments.Count > 0)
                            gameInfo.shipLoadouts[i].data.equipments = RemoveReplaceFromShipEquipment(isRemoveOp, gameInfo.shipLoadouts[i].data.equipments, targetIDVal, newIDVal);
                    }

            // Crew ships
            if (gameInfo.crew != null && gameInfo.crew.Count > 0)
                for (int i = 0; i < gameInfo.crew.Count; i++)
                    if (gameInfo.crew[i].aiChar != null && gameInfo.crew[i].aiChar.shipData != null)
                    {
                        if (gameInfo.crew[i].aiChar.shipData.cargo != null && gameInfo.crew[i].aiChar.shipData.cargo.Count > 0)
                            gameInfo.crew[i].aiChar.shipData.cargo = RemoveReplaceFromShipCargo(isRemoveOp, gameInfo.crew[i].aiChar.shipData.cargo, targetIDVal, newIDVal);
                        if (gameInfo.crew[i].aiChar.shipData.equipments != null && gameInfo.crew[i].aiChar.shipData.equipments.Count > 0)
                            gameInfo.crew[i].aiChar.shipData.equipments = RemoveReplaceFromShipEquipment(isRemoveOp, gameInfo.crew[i].aiChar.shipData.equipments, targetIDVal, newIDVal);
                    }

            // Mercenaries
            if (gameInfo.character != null && gameInfo.character.mercenaries != null && gameInfo.character.mercenaries.Count > 0)
                for (int i = 0; i < gameInfo.character.mercenaries.Count; i++)
                    if (gameInfo.character.mercenaries[i].shipData != null)
                    {
                        if (gameInfo.character.mercenaries[i].shipData.cargo != null && gameInfo.character.mercenaries[i].shipData.cargo.Count > 0)
                            gameInfo.character.mercenaries[i].shipData.cargo = RemoveReplaceFromShipCargo(isRemoveOp, gameInfo.character.mercenaries[i].shipData.cargo, targetIDVal, newIDVal);
                        if (gameInfo.character.mercenaries[i].shipData.equipments != null && gameInfo.character.mercenaries[i].shipData.equipments.Count > 0)
                            gameInfo.character.mercenaries[i].shipData.equipments = RemoveReplaceFromShipEquipment(isRemoveOp, gameInfo.character.mercenaries[i].shipData.equipments, targetIDVal, newIDVal);
                    }

            return gameInfo;
        }

        private static List<CargoItem> RemoveReplaceFromShipCargo(bool isRemoveOp, List<CargoItem> cargo, int targetIDVal, int newIDVal)
        {
            if (isRemoveOp)
                remRepOpFoundEquipment = cargo.RemoveAll(ci => ci.itemType == (int)SVUtil.GlobalItemType.equipment && ci.itemID == targetIDVal) > 0 || remRepOpFoundEquipment;
            else
                cargo.ForEach(ci => { if (ci.itemType == (int)SVUtil.GlobalItemType.equipment && ci.itemID == targetIDVal) ci.itemID = newIDVal; });

            return cargo;
        }

        private static List<InstalledEquipment> RemoveReplaceFromShipEquipment(bool isRemoveOp, List<InstalledEquipment> equipments, int targetIDVal, int newIDVal)
        {
            if (isRemoveOp)
                remRepOpFoundEquipment = equipments.RemoveAll(ie => ie.equipmentID == targetIDVal) > 0 || remRepOpFoundEquipment;
            else
                equipments.ForEach(ie => { if (ie.equipmentID == targetIDVal) ie.equipmentID = newIDVal; });

            return equipments;
        }

        private static GameDataInfo RemoveReplaceFromTowedObjects(bool isRemoveOp, GameDataInfo gameInfo, int targetIDVal, int newIDVal)
        {
            if (gameInfo.towedObjects != null && gameInfo.towedObjects.Count > 0)
                if (isRemoveOp)
                    remRepOpFoundEquipment = gameInfo.towedObjects.RemoveAll(to => to.driftingObject.itemType == (int)SVUtil.GlobalItemType.equipment && to.driftingObject.itemID == targetIDVal) > 0 || remRepOpFoundEquipment;
                else
                    gameInfo.towedObjects.ForEach(to => { if (to.driftingObject.itemType == (int)SVUtil.GlobalItemType.equipment && to.driftingObject.itemID == targetIDVal) to.driftingObject.itemID = newIDVal; });

            return gameInfo;
        }

        private static GameDataInfo RemoveReplaceFromMarkets(bool isRemoveOp, GameDataInfo gameInfo, int targetIDVal, int newIDVal)
        {
            // Stations list
            foreach (Station s in gameInfo.stationList)
                if (s.market != null && s.market.Count > 0)
                    if (isRemoveOp)
                        remRepOpFoundEquipment = s.market.RemoveAll(mi => mi.itemType == (int)SVUtil.GlobalItemType.equipment && mi.itemID == targetIDVal) > 0 || remRepOpFoundEquipment;
                    else
                        s.market.ForEach(mi => { if (mi.itemType == (int)SVUtil.GlobalItemType.equipment && mi.itemID == targetIDVal) mi.itemID = newIDVal; });

            // Arena market
            if (gameInfo.arenaData != null &&
                gameInfo.arenaData.currMarket != null &&
                gameInfo.arenaData.currMarket.Count > 0)
                if (isRemoveOp)
                    gameInfo.arenaData.currMarket.RemoveAll(mi => mi.itemType == (int)SVUtil.GlobalItemType.equipment && mi.itemID == targetIDVal);
                else
                    gameInfo.arenaData.currMarket.ForEach(mi => { if (mi.itemType == (int)SVUtil.GlobalItemType.equipment && mi.itemID == targetIDVal) mi.itemID = newIDVal; });

            return gameInfo;
        }

        private static GameDataInfo RemoveReplaceFromSectors(bool isRemoveOp, GameDataInfo gameInfo, int targetIDVal, int newIDVal)
        {
            if (gameInfo.sectors != null && gameInfo.sectors.Count > 0)
                foreach (TSector sec in gameInfo.sectors)
                    if (sec.driftingObjects != null && sec.driftingObjects.Count > 0)
                        if (isRemoveOp)
                            remRepOpFoundEquipment = sec.driftingObjects.RemoveAll(dro => dro.itemType == (int)SVUtil.GlobalItemType.equipment && dro.itemID == targetIDVal) > 0 || remRepOpFoundEquipment;
                        else
                            sec.driftingObjects.ForEach(dro => { if (dro.itemType == (int)SVUtil.GlobalItemType.equipment && dro.itemID == targetIDVal) dro.itemID = newIDVal; });

            if (gameInfo.lastSector != null)
                if (isRemoveOp)
                    remRepOpFoundEquipment = gameInfo.lastSector.driftingObjects.RemoveAll(dro => dro.itemType == (int)SVUtil.GlobalItemType.equipment && dro.itemID == targetIDVal) > 0 || remRepOpFoundEquipment;
                else
                    gameInfo.lastSector.driftingObjects.ForEach(dro => { if (dro.itemType == (int)SVUtil.GlobalItemType.equipment && dro.itemID == targetIDVal) dro.itemID = newIDVal; });

            return gameInfo;
        }
    }
}
