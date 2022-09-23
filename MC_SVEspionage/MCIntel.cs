using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace MC_SVEspionage
{
	internal class MCIntel
	{		
		internal const int maxIntels = 5;
		private const string stationNamePlaceholder = "<STATIONNAME>";
		private const string description = "A collection of various trade records and security protocols.\n " + stationNamePlaceholder;
		private const int bpItemID = 54; // for gameobj + sprite

		private static int startID;

		internal static List<Item> Load(List<Item> items)
        {
			startID = SVItemUtil.GetNextID(items);

			List<Item> intels = new List<Item>();
			for (int i = 0; i < maxIntels; i++)
				intels.Add(CreateIntel(startID + i));

			if (Main.data.intelInCargo.Count > 0)
			{
				Dictionary<int, int> ids = new Dictionary<int, int>();
				for (int i = 0; i < Main.data.intelInCargo.Count; i++)
				{
					intels[i].description = Main.data.intelInCargo[i].description;
					ids.Add(Main.data.intelInCargo[i].id, intels[i].id);
                    Main.data.intelInCargo[i].id = intels[i].id;
				}

				if (ids.Count > 0)
					GameData.data.spaceShipData.cargo = SVUtil.RemoveReplaceFromShipCargo(false, GameData.data.spaceShipData.cargo, ids, SVUtil.GlobalItemType.genericitem);
			}

			items.AddRange(intels);

			return items;
		}

		internal static Item AddIntel(string stationName)
        {
			int newIntelIndex = Main.data.intelInCargo.Count;

			if (newIntelIndex == maxIntels)
				return null;

			Item newIntel = ItemDB.GetItem(startID + newIntelIndex);
			newIntel.description = description.Replace(stationNamePlaceholder, stationName);
			Main.data.intelInCargo.Add(newIntel);

			SVItemUtil.ReplaceInDB(ItemDB.GetItem(startID + newIntelIndex), newIntel);

			return newIntel;
		}

		internal static List<CargoItem> RemoveIntel(List<CargoItem> cargo, string stationName)
        {
			int intelIndex = -1;
			if (Main.data.intelInCargo.Count > 0)
				for (int i = 0; i < Main.data.intelInCargo.Count; i++)
					if (Main.data.intelInCargo[i].description.Contains(stationName))
					{
						intelIndex = i;
						break;
					}

			if (intelIndex == -1)
				return cargo;

			Main.data.intelInCargo.RemoveAt(intelIndex);
			return SVUtil.RemoveReplaceFromShipCargo(true, cargo, startID + intelIndex, 0, SVUtil.GlobalItemType.genericitem);
        }

		private static Item CreateIntel(int itemID)
		{
			Item intel = new Item();
			intel.id = itemID;
			intel.refName = "Station Intel";
			intel.rarity = 1;
			intel.levelPlus = 0;
			intel.weight = 0f;
			intel.basePrice = 1000f;
			intel.priceVariation = 0f;
			intel.tradeChance = new int[7] { 0, 0, 0, 0, 0, 0, 0 };
			intel.tradeQuantity = 0;
			intel.type = ItemType.Data;
			intel.gameObj = ItemDB.GetItem(bpItemID).gameObj;
			intel.sprite = ItemDB.GetItem(bpItemID).sprite;
			intel.askedInQuests = false;
			intel.canBeStashed = false;
			intel.itemName = "Station Intel";
			intel.description = description;
			intel.canUpgradeToTier = ItemRarity.Poor_0;
			intel.craftable = false;
			intel.craftingYield = 0;
			intel.craftingLevelAffectsYield = false;
			intel.craftingMaterials = new List<CraftMaterial>();
			intel.teachItemBlueprints = new int[0];
			return intel;
		}
	}
}
