using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace MC_SVTargetScanner
{
    internal class MCIntel
    {
        private const int startingItemID = 1000;
		private const int maxData = 10;
		private const int bpItemID = 54; // for gameobj + sprite

		private Item CreateItem(int itemID, string description)
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
