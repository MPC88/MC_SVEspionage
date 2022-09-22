using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace MC_SVTargetScanner
{
    public class MCTargetScanner
    {
        internal static int sysInfilEffectTextIndex = 0;
        private static GameObject buffGO = null;
        private const int equipID = 1000;
        private const int basicScannerEquipID = 20; // For sprite
        private const string effectText = "<b>Systems Infiltration: <par1></color></b>"; // Only required if effect does not already exist.  For existing type IDs, check language file.                
        private static Equipment equipment = null;

        internal static List<Equipment> EquipmentDBLoadDatabase_Post(List<Equipment> equipments)
        {
            if (Main.cfgEquipID.Value == -1)
                GenerateID();

            if (!Main.patchedText)
                AddToEffectsTextSection();

            if (equipment == null)
                MCTargetScanner.CreateEquipment();

            if (equipments != null)
                equipments.Add(equipment);

            return equipments;
        }

        private static void GenerateID()
        {
            if (Main.cfgEquipID.Value == -1)
            {
                List<Equipment> equipmentList = AccessTools.StaticFieldRefAccess<EquipmentDB, List<Equipment>>("equipments");
                if (equipmentList == null || equipmentList.Count == 0)
                    return;

                int id = equipID;
                while (Main.cfgEquipID.Value == -1)
                {
                    bool exists = false;
                    foreach (Equipment e in equipmentList)
                    {
                        if (e.id == id)
                        {
                            exists = true;
                            id++;
                            break;
                        }
                    }
                    if (!exists)
                        Main.cfgEquipID.Value = id;
                }
            }
        }

        internal static void CreateEquipment()
        {
            Equipment targetScanner = ScriptableObject.CreateInstance<Equipment>();
            targetScanner.name = Main.cfgEquipID + ".TargetScanner";
            targetScanner.id = Main.cfgEquipID.Value;
            targetScanner.refName = "Target Scanner";
            targetScanner.minShipClass = ShipClassLevel.Shuttle;
            targetScanner.activated = true;
            targetScanner.enableChangeKey = true;
            targetScanner.space = 1;
            targetScanner.energyCost = 80f;
            targetScanner.energyCostPerShipClass = false;
            targetScanner.rarityCostMod = 0f;
            targetScanner.techLevel = 0;
            targetScanner.sortPower = 0;
            targetScanner.massChange = 0;
            targetScanner.type = EquipmentType.Utility;
            targetScanner.effects = new List<Effect>() { new Effect() { type = sysInfilEffectTextIndex, description = "Range", value = 10f, mod = 1f, uniqueLevel = 2 } };
            targetScanner.uniqueReplacement = true;
            targetScanner.rarityMod = 10f;
            targetScanner.sellChance = 100;
            targetScanner.repReq = new ReputationRequisite() { factionIndex = 0, repNeeded = 0 };
            targetScanner.dropLevel = DropLevel.Normal;
            targetScanner.lootChance = 100;
            targetScanner.spawnInArena = false;
            targetScanner.sprite = EquipmentDB.GetEquipment(basicScannerEquipID).sprite;
            targetScanner.activeEquipmentIndex = Main.cfgEquipID.Value;
            targetScanner.defaultKey = KeyCode.Alpha1;
            targetScanner.requiredItemID = -1;
            targetScanner.requiredQnt = 0;
            targetScanner.equipName = "Target Scanner";
            targetScanner.description = "Ship: Reveal target information.\nStation: Hack station systems to obtain intel.";
            targetScanner.craftingMaterials = null;
            if (buffGO == null)
                MakeBuffGO(targetScanner);
            targetScanner.buff = buffGO;

            equipment = targetScanner;
        }

        internal static void AddToEffectsTextSection()
        {
            LanguageTextStruct[] lang = AccessTools.StaticFieldRefAccess<Lang, LanguageTextStruct[]>("section");
            lang[Main.langEffectTextSection].text.Add(effectText);
            sysInfilEffectTextIndex = lang[Main.langEffectTextSection].text.Count - 1;            
        }

        private static void MakeBuffGO(Equipment equip)
        {
            buffGO = new GameObject { name = "ShipScanner" };
            buffGO.AddComponent<BuffControl>();
            buffGO.GetComponent<BuffControl>().owner = null;
            buffGO.GetComponent<BuffControl>().activeEquipment = MakeActiveEquip(
                equip, null, equip.defaultKey, 1, 0);
            buffGO.AddComponent<BuffEnergyChange>();
            buffGO.AddComponent<BuffMCTargetScanner>();
        }

        private static AE_MCTargetScanner MakeActiveEquip(Equipment equipment, SpaceShip ss, KeyCode key, int rarity, int qnt)
        {
            AE_MCTargetScanner targetScannerAE = new AE_MCTargetScanner
            {
                id = equipment.id,
                rarity = rarity,
                key = key,
                ss = ss,
                isPlayer = (ss != null && ss.CompareTag("Player")),
                equipment = equipment,
                qnt = qnt
            };
            targetScannerAE.active = false;
            return targetScannerAE;
        }

        internal static ActiveEquipment ActivateDeactivate_Pre(ActiveEquipment __instance)
        {
            if (buffGO == null)
                MakeBuffGO(__instance.equipment);
            __instance.equipment.buff = buffGO;

            return __instance;
        }

        internal static AE_MCTargetScanner ActiveEquipmentAdd_Pre(Equipment equipment, SpaceShip ss, KeyCode key, int rarity, int qnt)
        {
            AE_MCTargetScanner ae = MakeActiveEquip(equipment, ss, key, rarity, qnt);
            ss.activeEquips.Add(ae);
            ae.AfterConstructor();
            return ae;
        }
    }

    public class BuffMCTargetScanner : BuffBase
    {
        public const float scanTime = 5f;
        private SpaceShip targetShip;
        private Station targetStation;
        private float timeCount = 0;

        protected override void Setup()
        {
            base.Setup();
            this.targetSS = this.buffControl.owner;
        }

        private void Update()
        {
            if ((this.timeCount -= Time.deltaTime) <= 0f)
                ScanComplete();
        }

        protected override void Begin()
        {
            base.Begin();
            this.targetShip = CurrentPlayerTargetShip();
            this.targetStation = CurrentPlayerTargetStation();
            this.timeCount = BuffMCTargetScanner.scanTime;
            if (this.targetShip == null && this.targetStation == null)
            {
                SideInfo.AddMsg("Scan failed: No target.");
                this.buffControl.activeEquipment.ActivateDeactivate(false, this.buffControl.owner.transform);
            }
        }

        protected override void End()
        {
            base.End();
        }

        private void ScanComplete()
        {
            if (targetStation != null)
            {
                Station endTarget = CurrentPlayerTargetStation();

                if (endTarget == null ||
                    this.targetStation.id != endTarget.id)
                    SideInfo.AddMsg("Scan failed: Target lost.");
                else
                    SideInfo.AddMsg("Scanned station: " + this.targetStation.stationName + " " + this.targetStation.id + " " + GameData.data.sectors[this.targetStation.sectorIndex].coords);
            }
            else if (targetShip != null)
            {
                SpaceShip endTarget = CurrentPlayerTargetShip();

                if (endTarget == null ||
                    this.targetShip.gameObject.GetInstanceID() != endTarget.gameObject.GetInstanceID())
                    SideInfo.AddMsg("Scan failed: Target lost.");
                else
                    ScanSuccessShip();
            }

            // Auto deactivate
            this.buffControl.activeEquipment.ActivateDeactivate(false, this.buffControl.owner.transform);
        }

        private void ScanSuccessShip()
        {
            // Currently scanner output is based only on rarity.
            // Potential to modify to use Systems Infiltration effect, but only really
            // worth if there is an associated defence stat or different models of the equipment.
            // For now, the effect is just fluff.

            SideInfo.AddMsg("Target: <color=yellow>" + targetShip.name + "</color>");
            SideInfo.AddMsg("Model: <color=yellow>" + targetShip.shipData.GetShipModelData().modelName + " " + "(C" + targetShip.sizeClass + ")</color>");
            SideInfo.AddMsg("Status: <color=red>" + Mathf.RoundToInt(targetShip.currHP) + "</color> / <color=lightblue>" + Mathf.RoundToInt(targetShip.stats.currShield) + "</color>");
            SideInfo.AddMsg("Range: <color=yellow>" + Mathf.RoundToInt(Vector3.Distance(targetSS.gameObject.transform.position, targetShip.gameObject.transform.position)).ToString() + "</color>");
            if (buffControl.activeEquipment.rarity >= (int)ItemRarity.Uncommon_2)
            {
                // Weapons
                Dictionary<string, int> weaponList = new Dictionary<string, int>();
                for (int i = 0; i < targetShip.weapons.Count; i++)
                    if (weaponList.ContainsKey(targetShip.weapons[i].name))
                        weaponList[targetShip.weapons[i].name]++;
                    else
                        weaponList.Add(targetShip.weapons[i].name, 1);
                SideInfo.AddMsg("Weapons:");
                foreach (string wName in weaponList.Keys)
                {
                    if (buffControl.activeEquipment.rarity >= (int)ItemRarity.Rare_3)
                        SideInfo.AddMsg("<color=yellow>" + wName + " x" + weaponList[wName] + "</color>");
                    else
                        SideInfo.AddMsg("<color=yellow>" + wName + "</color>");
                }

                // Active equipment
                if (buffControl.activeEquipment.rarity >= (int)ItemRarity.Epic_4)
                {
                    SideInfo.AddMsg("Active Equipment:");
                    Dictionary<string, int> equipmentList = new Dictionary<string, int>();
                    for (int i = 0; i < targetShip.activeEquips.Count; i++)
                        if (equipmentList.ContainsKey(targetShip.activeEquips[i].equipment.equipName))
                            equipmentList[targetShip.activeEquips[i].equipment.equipName]++;
                        else
                            equipmentList.Add(targetShip.activeEquips[i].equipment.equipName, 1);
                    foreach (string eName in equipmentList.Keys)
                    {
                        if (buffControl.activeEquipment.rarity >= (int)ItemRarity.Legendary_5)
                            SideInfo.AddMsg("<color=yellow>" + eName + " x" + equipmentList[eName] + "</color>");
                        else
                            SideInfo.AddMsg("<color=yellow>" + eName + "</color>");
                    }
                }
            }
        }

        private SpaceShip CurrentPlayerTargetShip()
        {
            SpaceShip target = null;
            if (this.targetSS != null)
            {
                PlayerControl pc = this.targetSS.gameObject.GetComponent<PlayerControl>();
                if (pc != null && pc.target != null)
                    target = pc.target.gameObject.GetComponent<SpaceShip>();
            }

            return target;
        }

        private Station CurrentPlayerTargetStation()
        {
            Station target = null;
            if (this.targetSS != null)
            {
                PlayerControl pc = this.targetSS.gameObject.GetComponent<PlayerControl>();
                if (pc != null && pc.target != null)
                {
                    Transform dcTrans = pc.target.gameObject.transform.Find("DockingControl");
                    if (dcTrans != null)
                    {
                        DockingControl dc = dcTrans.GetComponent<DockingControl>();
                        if (dc != null)
                            target = dc.station;
                    }
                }
            }

            return target;
        }
    }

    public class AE_MCTargetScanner : AE_BuffBased
    {
        protected override bool showBuffIcon
        {
            get
            {
                return this.isPlayer;
            }
        }

        public AE_MCTargetScanner()
        {
            this.targetIsSelf = true;
            this.saveState = false;
        }

        protected override bool AfterInstantiateBuffGO()
        {
            base.AddEnergyChange(1f);
            return true;
        }

        public override void AfterActivate()
        {
            base.ShowBuffIconGO("Scanning target...", BuffMCTargetScanner.scanTime);
        }
    }
}