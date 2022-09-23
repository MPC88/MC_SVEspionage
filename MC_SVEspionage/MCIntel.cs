using System.Collections.Generic;
using UnityEngine;

namespace MC_SVEspionage
{
	internal class MCIntel
	{		
		internal const int maxIntels = 5;		
		private const string itemName = "Station Intel";
		private const string stationNamePlaceholder = "<STATIONNAME>";
		private const string description = "A collection of economic records and security protocols.\n " + stationNamePlaceholder;
		private const int bpItemID = 54; // for gameobj + sprite		

		internal static int startID = -1;

		internal static List<Item> Load(List<Item> items)
		{
			// Check if replacing or adding for first time
			bool replacing = false;
			Item tryGetItem = ItemDB.GetItem(startID);
			if (tryGetItem != null && tryGetItem.itemName.Equals(itemName))
				replacing = true;

			if (!replacing)
				startID = SVItemUtil.GetNextID(items);

			// Now create
			List<Item> intels = new List<Item>();
			for (int i = 0; i < maxIntels; i++)
				intels.Add(CreateIntel(startID + i));

			if (Main.data.intelInCargo.Count > 0)
			{
				Dictionary<int, int> ids = new Dictionary<int, int>();
				for (int i = 0; i < Main.data.intelInCargo.Count; i++)
				{
					intels[i].description = description.Replace(stationNamePlaceholder, Main.data.intelInCargo[i].stationName);
					ids.Add(Main.data.intelInCargo[i].id, intels[i].id);
                    Main.data.intelInCargo[i].id = intels[i].id;
				}

				if (ids.Count > 0)
					GameData.data.spaceShipData.cargo = SVUtil.RemoveReplaceFromShipCargo(false, GameData.data.spaceShipData.cargo, ids, SVUtil.GlobalItemType.genericitem);
			}

			if (replacing)
				for (int i = 0; i < maxIntels; i++)
					SVItemUtil.ReplaceInDB(startID + i, intels[i]);
			else
				items.AddRange(intels);

			return items;
		}

		internal static void AddIntel(string stationName, SpaceShip ss)
        {
			int intelIndex = Main.data.intelInCargo.Count;

			if (intelIndex == maxIntels)
			{
				SideInfo.AddMsg("Scanner memory at capacity.  Data discarded.");
				return;
			}

			if (Main.data.CargoContainsIntelForStation(stationName))
            {
				SideInfo.AddMsg("Existing data for station overwritten.");
				return;
			}

			if (ss == null)
				return;

			CargoSystem cs = ss.GetComponent<CargoSystem>();
			if (cs != null)
			{
				Item intel = ItemDB.GetItem(startID + intelIndex);
				intel.description = description.Replace(stationNamePlaceholder, stationName);				
				SVItemUtil.ReplaceInDB(startID + intelIndex, intel);				
				cs.StoreItem((int)SVUtil.GlobalItemType.genericitem, intel.id, intel.rarity, 1, 0f, -1, -1, -1);
			}
		}

		internal static void RemoveIntel(string stationName)
        {
			int intelIndex = -1;
			if (Main.data.intelInCargo.Count > 0)
				for (int i = 0; i < Main.data.intelInCargo.Count; i++)
					if (Main.data.intelInCargo[i].stationName.Equals(stationName))
					{
						intelIndex = i;
						break;
					}

			if (intelIndex == -1)
				return;

			DoRemoveIntel(intelIndex);
		}

		internal static void RemoveIntel(int itemID)
        {
			int intelIndex = -1;
			if (Main.data.intelInCargo.Count > 0)
				for (int i = 0; i < Main.data.intelInCargo.Count; i++)
					if (Main.data.intelInCargo[i].id == itemID)
					{
						intelIndex = i;
						break;
					}

			if (intelIndex == -1)
				return;

			DoRemoveIntel(intelIndex);
		}

		private static void DoRemoveIntel(int index)
        {
			Main.data.intelInCargo.RemoveAt(index);
			Item intel = ItemDB.GetItem(startID + index);
			intel.description = description;
			SVItemUtil.ReplaceInDB(startID + index, intel);
		}

		private static Item CreateIntel(int itemID)
		{
			Item intel = ScriptableObject.CreateInstance<Item>();
			intel.id = itemID;
			intel.name = itemID + "." + itemName;
			intel.refName = itemName;
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
			intel.itemName = itemName;
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
