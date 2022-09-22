using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace MC_SVTargetScanner
{
    internal class Uninstaller
    {
        internal static bool running = false;

        private enum GlobalItemType { none, weapon, equipment, genericitem, ship }
        private static bool foundItem = false;

        internal static string[] GetSavesList()
        {
            string[] saveFiles = new string[1];
            saveFiles[0] = "NO SAVES FOUND";
            try
            {
                saveFiles = Directory.GetFiles(Application.dataPath + GameData.saveFolderName, "SaveGameData_??.dat");
                for (int i = 0; i < saveFiles.Length; i++)
                    saveFiles[i] = Path.GetFileName(saveFiles[i]);
            }
            catch
            {
                saveFiles[0] = "NO SAVES FOUND";
            }
            return saveFiles;
        }

        internal static void RemoveReplaceEquipment(bool isRemoveOp, int targetIDVal, int newIDVal, string saveFilePathVal)
        {
            running = true;
            foundItem = false;
            bool failed = false;
            string bkPath = "";
            GameDataInfo gdi = null;

            // Backup save
            try
            {
                bkPath = CreateBackup(saveFilePathVal);
            }
            catch (Exception ex)
            {
                failed = true;
                Main.cfgStatus.Value = "Failed to create save game backup.  Aborted.";
            }

            // Load save
            if (!failed)
            {
                try
                {
                    gdi = LoadGame(saveFilePathVal);
                }
                catch
                {
                    failed = true;
                    Main.cfgStatus.Value = "Failed to load save game: '" + saveFilePathVal + "'.  Aborted.";
                }
            }

            // Ship equip, ship cargo, stash, stored ships, towed obj
            if (!failed)
            {
                try
                {
                    gdi = Equip_RemRepFromShips(isRemoveOp, gdi, targetIDVal, newIDVal);
                    gdi = Equip_RemRepFromTowedObjects(isRemoveOp, gdi, targetIDVal, newIDVal);
                }
                catch
                {
                    failed = true;
                    if (isRemoveOp)
                        Main.cfgStatus.Value = "Failed to remove equipment: '" + targetIDVal.ToString() + "' from ship loadouts (cargo/equipments).  Aborted.";
                    else
                        Main.cfgStatus.Value = "Failed to replace equipment: '" + targetIDVal.ToString() + "' with '" + newIDVal.ToString() + "' in ship loadouts (cargo/equipments).  Aborted.";
                }
            }

            // Markets and sectors
            if (!failed)
            {
                try
                {
                    gdi = Equip_RemRepFromMarkets(isRemoveOp, gdi, targetIDVal, newIDVal);
                    gdi = Equip_RemRepFromSectors(isRemoveOp, gdi, targetIDVal, newIDVal);
                }
                catch
                {
                    failed = true;
                    if (isRemoveOp)
                        Main.cfgStatus.Value = "Failed to remove equipment: '" + targetIDVal.ToString() + "' from station markets.";
                    else
                        Main.cfgStatus.Value = "Failed to replace equipment: '" + targetIDVal.ToString() + "' with '" + newIDVal.ToString() + "' in station markets.";
                }
            }

            // Save
            if (!failed && foundItem)
            {
                try
                {
                    Save(gdi, saveFilePathVal);
                }
                catch
                {
                    failed = true;
                    Main.cfgStatus.Value = "Saving modified game data failed.  Restore your backup from '" + Main.savePathStr + Main.backupPathStr + "' in your save games folder.  Just remove '_yyyy-MM-dd--HH-mm' from the filename.  Aborted.";
                }
            }

            // Delete temp files
            if (!failed)
            {
                try
                {
                    Cleanup(saveFilePathVal);
                }
                catch
                {
                    Main.cfgStatus.Value = "Cleanup failed.  '" + Main.savePathStr + Main.tempFilePathStr + "' may still exist in your save games folder.  You can delete this and the directory.  Sorry!";
                }
            }

            // Victory!
            if (!failed)
            {
                if (foundItem)
                    Main.cfgStatus.Value = "Done. Save backup: " + bkPath;
                else
                    Main.cfgStatus.Value = "Item ID " + targetIDVal + " not found in selected save game.";
            }
        }

        private static string CreateBackup(string saveFilePathVal)
        {
            string backupPath = Path.GetDirectoryName(saveFilePathVal) + Main.savePathStr + Main.backupPathStr + Path.GetFileNameWithoutExtension(saveFilePathVal) + "_" + DateTime.Now.ToString("yyyy-MM-dd--HH-mm") + ".dat";
            if (File.Exists(backupPath))
                throw new Exception();

            if (!Directory.Exists(Path.GetDirectoryName(backupPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));

            File.Copy(saveFilePathVal, backupPath, false);
            return backupPath;
        }

        private static GameDataInfo LoadGame(string saveFilePathVal)
        {
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            FileStream fileStream = File.Open(saveFilePathVal, FileMode.Open);
            GameDataInfo gameInfo = (GameDataInfo)binaryFormatter.Deserialize(fileStream);
            fileStream.Close();

            if (gameInfo == null)
                throw new Exception();

            return gameInfo;
        }

        private static GameDataInfo Equip_RemRepFromShips(bool isRemoveOp, GameDataInfo gameInfo, int targetIDVal, int newIDVal)
        {
            // Space ship data - player            
            if (gameInfo.spaceShipData != null)
            {
                if (gameInfo.spaceShipData.cargo != null && gameInfo.spaceShipData.cargo.Count > 0)
                    gameInfo.spaceShipData.cargo = Equip_RemRepShipCargo(isRemoveOp, gameInfo.spaceShipData.cargo, targetIDVal, newIDVal);
                if (gameInfo.spaceShipData.equipments != null && gameInfo.spaceShipData.equipments.Count > 0)
                    gameInfo.spaceShipData.equipments = Equip_RemRepShipEquipments(isRemoveOp, gameInfo.spaceShipData.equipments, targetIDVal, newIDVal);
            }
            else // No player = no bueno                
                throw new Exception();

            // Stored ships
            if (gameInfo.shipLoadouts != null && gameInfo.shipLoadouts.Count > 0)
                for (int i = 0; i < gameInfo.shipLoadouts.Count; i++)
                    if (gameInfo.shipLoadouts[i].data != null)
                    {
                        if (gameInfo.shipLoadouts[i].data.cargo != null && gameInfo.shipLoadouts[i].data.cargo.Count > 0)
                            gameInfo.shipLoadouts[i].data.cargo = Equip_RemRepShipCargo(isRemoveOp, gameInfo.shipLoadouts[i].data.cargo, targetIDVal, newIDVal);
                        if (gameInfo.shipLoadouts[i].data.equipments != null && gameInfo.shipLoadouts[i].data.equipments.Count > 0)
                            gameInfo.shipLoadouts[i].data.equipments = Equip_RemRepShipEquipments(isRemoveOp, gameInfo.shipLoadouts[i].data.equipments, targetIDVal, newIDVal);
                    }

            // Crew ships
            if (gameInfo.crew != null && gameInfo.crew.Count > 0)
                for (int i = 0; i < gameInfo.crew.Count; i++)
                    if (gameInfo.crew[i].aiChar != null && gameInfo.crew[i].aiChar.shipData != null)
                    {
                        if (gameInfo.crew[i].aiChar.shipData.cargo != null && gameInfo.crew[i].aiChar.shipData.cargo.Count > 0)
                            gameInfo.crew[i].aiChar.shipData.cargo = Equip_RemRepShipCargo(isRemoveOp, gameInfo.crew[i].aiChar.shipData.cargo, targetIDVal, newIDVal);
                        if (gameInfo.crew[i].aiChar.shipData.equipments != null && gameInfo.crew[i].aiChar.shipData.equipments.Count > 0)
                            gameInfo.crew[i].aiChar.shipData.equipments = Equip_RemRepShipEquipments(isRemoveOp, gameInfo.crew[i].aiChar.shipData.equipments, targetIDVal, newIDVal);
                    }

            // Mercenaries
            if (gameInfo.character != null && gameInfo.character.mercenaries != null && gameInfo.character.mercenaries.Count > 0)
                for (int i = 0; i < gameInfo.character.mercenaries.Count; i++)
                    if (gameInfo.character.mercenaries[i].shipData != null)
                    {
                        if (gameInfo.character.mercenaries[i].shipData.cargo != null && gameInfo.character.mercenaries[i].shipData.cargo.Count > 0)
                            gameInfo.character.mercenaries[i].shipData.cargo = Equip_RemRepShipCargo(isRemoveOp, gameInfo.character.mercenaries[i].shipData.cargo, targetIDVal, newIDVal);
                        if (gameInfo.character.mercenaries[i].shipData.equipments != null && gameInfo.character.mercenaries[i].shipData.equipments.Count > 0)
                            gameInfo.character.mercenaries[i].shipData.equipments = Equip_RemRepShipEquipments(isRemoveOp, gameInfo.character.mercenaries[i].shipData.equipments, targetIDVal, newIDVal);
                    }

            return gameInfo;
        }

        private static List<CargoItem> Equip_RemRepShipCargo(bool isRemoveOp, List<CargoItem> cargo, int targetIDVal, int newIDVal)
        {
            if (isRemoveOp)
                foundItem = cargo.RemoveAll(ci => ci.itemType == (int)GlobalItemType.equipment && ci.itemID == targetIDVal) > 0 || foundItem;
            else
                cargo.ForEach(ci => { if (ci.itemType == (int)GlobalItemType.equipment && ci.itemID == targetIDVal) ci.itemID = newIDVal; });

            return cargo;
        }

        private static List<InstalledEquipment> Equip_RemRepShipEquipments(bool isRemoveOp, List<InstalledEquipment> equipments, int targetIDVal, int newIDVal)
        {
            if (isRemoveOp)
                foundItem = equipments.RemoveAll(ie => ie.equipmentID == targetIDVal) > 0 || foundItem;
            else
                equipments.ForEach(ie => { if (ie.equipmentID == targetIDVal) ie.equipmentID = newIDVal; });

            return equipments;
        }

        private static GameDataInfo Equip_RemRepFromTowedObjects(bool isRemoveOp, GameDataInfo gameInfo, int targetIDVal, int newIDVal)
        {
            if (gameInfo.towedObjects != null && gameInfo.towedObjects.Count > 0)
                if (isRemoveOp)
                    foundItem = gameInfo.towedObjects.RemoveAll(to => to.driftingObject.itemType == (int)GlobalItemType.equipment && to.driftingObject.itemID == targetIDVal) > 0 || foundItem;
                else
                    gameInfo.towedObjects.ForEach(to => { if (to.driftingObject.itemType == (int)GlobalItemType.equipment && to.driftingObject.itemID == targetIDVal) to.driftingObject.itemID = newIDVal; });

            return gameInfo;
        }

        private static GameDataInfo Equip_RemRepFromMarkets(bool isRemoveOp, GameDataInfo gameInfo, int targetIDVal, int newIDVal)
        {
            // Stations list
            foreach (Station s in gameInfo.stationList)
                if (s.market != null && s.market.Count > 0)
                    if (isRemoveOp)
                        foundItem = s.market.RemoveAll(mi => mi.itemType == (int)GlobalItemType.equipment && mi.itemID == targetIDVal) > 0 || foundItem;
                    else
                        s.market.ForEach(mi => { if (mi.itemType == (int)GlobalItemType.equipment && mi.itemID == targetIDVal) mi.itemID = newIDVal; });

            // Arena market
            if (gameInfo.arenaData != null &&
                gameInfo.arenaData.currMarket != null &&
                gameInfo.arenaData.currMarket.Count > 0)
                if (isRemoveOp)
                    gameInfo.arenaData.currMarket.RemoveAll(mi => mi.itemType == (int)GlobalItemType.equipment && mi.itemID == targetIDVal);
                else
                    gameInfo.arenaData.currMarket.ForEach(mi => { if (mi.itemType == (int)GlobalItemType.equipment && mi.itemID == targetIDVal) mi.itemID = newIDVal; });

            return gameInfo;
        }

        private static GameDataInfo Equip_RemRepFromSectors(bool isRemoveOp, GameDataInfo gameInfo, int targetIDVal, int newIDVal)
        {
            if (gameInfo.sectors != null && gameInfo.sectors.Count > 0)
                foreach (TSector sec in gameInfo.sectors)
                    if (sec.driftingObjects != null && sec.driftingObjects.Count > 0)
                        if (isRemoveOp)
                            foundItem = sec.driftingObjects.RemoveAll(dro => dro.itemType == (int)GlobalItemType.equipment && dro.itemID == targetIDVal) > 0 || foundItem;
                        else
                            sec.driftingObjects.ForEach(dro => { if (dro.itemType == (int)GlobalItemType.equipment && dro.itemID == targetIDVal) dro.itemID = newIDVal; });

            if (gameInfo.lastSector != null)
                if (isRemoveOp)
                    foundItem = gameInfo.lastSector.driftingObjects.RemoveAll(dro => dro.itemType == (int)GlobalItemType.equipment && dro.itemID == targetIDVal) > 0 || foundItem;
                else
                    gameInfo.lastSector.driftingObjects.ForEach(dro => { if (dro.itemType == (int)GlobalItemType.equipment && dro.itemID == targetIDVal) dro.itemID = newIDVal; });

            return gameInfo;
        }

        private static void Save(GameDataInfo gameInfo, string saveFilePathVal)
        {
            string tempPath = Path.GetDirectoryName(saveFilePathVal) + Main.savePathStr + Main.tempFilePathStr;

            if (!Directory.Exists(Path.GetDirectoryName(tempPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath));

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            BinaryFormatter binaryFormatter = new BinaryFormatter();
            FileStream fileStream = null;
            fileStream = File.Create(tempPath);
            binaryFormatter.Serialize(fileStream, gameInfo);
            fileStream.Close();

            File.Copy(tempPath, saveFilePathVal, true);
        }

        private static void Cleanup(string saveFilePathVal)
        {
            string tempPath = Path.GetDirectoryName(saveFilePathVal) + Main.savePathStr + Main.tempFilePathStr;
            File.Delete(tempPath);
            Directory.Delete(Path.GetDirectoryName(tempPath));
        }
    }
}
