#region License (GPL v2)
/*
    QRadar - ALlow players to spawn a geiger counter and use it
        to find and describe items, or simply find items without one

    Copyright (c) 2023 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License v2.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License Information (GPL v2)
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("QRadar", "RFC1920", "1.0.9")]
    [Description("Simple player radar for world objects")]
    internal class QRadar : RustPlugin
    {
        private ConfigData configData;
        private const string permUse = "qradar.use";
        private const string permSeeNPC = "qradar.npc";
        private const string permHeld = "qradar.held";
        private List<ulong> playerUse = new();

        private Dictionary<ulong, uint> issuedCounters = new();

        [PluginReference]
        private readonly Plugin Backpacks;
        //private readonly Plugin Friends, Clans;

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        private void LMessage(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["addedto"] = "A geiger counter has been added to {0}",
                ["alreadyin"] = "You already have a geigercounter in {0}",
                ["issued"] = "You have already been issued a geiger counter today",
                ["backpack"] = "your backpack",
                ["belt"] = "your belt",
                ["main"] = "your main inventory",
                ["noroom"] = "You have no room to store a geigercounter",
                ["tooquick"] = "You must wait {0} seconds between radar bursts.",
                ["notauthorized"] = "You don't have permission to do that !!"
            }, this);
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permHeld, this);
            LoadConfigVariables();
            AddCovalenceCommand("qradar", "cmdQRadar");
            AddCovalenceCommand("qcounter", "cmdQCounter");
        }

        private object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
        {
            if (configData?.specialHandlingForGC == false) return null;
            if (item.info.itemid == 999690781)
            {
                // Preventing moving GC to anything other than the player's inventory or their Backpack.
                BasePlayer player = playerLoot.GetComponent<BasePlayer>();
                ItemContainer container = player.inventory.FindContainer(targetContainer);
                if (container == player.inventory.containerBelt || container == player.inventory.containerMain)
                {
                    return null;
                }
                else if (Backpacks)
                {
                    ItemContainer backpack = Backpacks?.Call("API_GetBackpackContainer", player.userID) as ItemContainer;
                    if (backpack != null && container == backpack) return null;
                }
                return true;
            }
            return null;
        }

        private void OnItemDropped(Item item, BaseEntity entity)
        {
            // Destroy GC when dropped
            OnPlayerDropActiveItem(null, item);
        }
        private void OnPlayerDropActiveItem(BasePlayer player, Item item)
        {
            if (configData?.specialHandlingForGC == false) return;
            // Destroy GC when dropped while active (on death, etc.)
            if (item.info.itemid == 999690781)
            {
                item.GetHeldEntity()?.Kill();
                item.DoRemove();
            }
        }

        private object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            // Destroy GC on death
            if (!player.userID.IsSteamId()) return null;
            OnPlayerDisconnected(player);
            return null;
        }

        private void OnPlayerConnected(BasePlayer player) => OnPlayerDisconnected(player);
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (configData?.specialHandlingForGC == false) return;
            if (!player.userID.IsSteamId()) return;
            // Destroy GC on connect/disconnect
            Item foundInBelt = player.inventory.containerBelt.FindItemsByItemID(999690781).FirstOrDefault();// "geiger.counter");
            foundInBelt?.GetHeldEntity()?.Kill();
            foundInBelt?.DoRemove();
            player.inventory.containerBelt.MarkDirty();

            Item foundInMain = player.inventory.containerMain.FindItemsByItemID(999690781).FirstOrDefault();
            foundInMain?.GetHeldEntity()?.Kill();
            foundInMain?.DoRemove();
            player.inventory.containerMain.MarkDirty();

            Item foundInBackpack = null;
            if (Backpacks)
            {
                ItemContainer backpack = Backpacks?.Call("API_GetBackpackContainer", player.userID) as ItemContainer;
                if (backpack != null)
                {
                    for (int i = 0; i < backpack.itemList.Count; i++)
                    {
                        if (backpack.itemList[i].info.itemid == 999690781)
                        {
                            foundInBackpack = backpack.itemList[i];
                        }
                    }
                    foundInBackpack?.GetHeldEntity()?.Kill();
                    foundInBackpack?.DoRemove();
                    backpack.MarkDirty();
                }
            }
            // Let them create a new one when they rejoin before plugin reload.
            issuedCounters.Remove(player.userID);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;

            GeigerCounter held = player.GetHeldEntity() as GeigerCounter;
            if (held == null) return;
            if (!permission.UserHasPermission(player.UserIDString, permHeld)) return;

            if (input.WasJustPressed(BUTTON.FIRE_PRIMARY)) RunAreaScan(player.IPlayer, true);
            else if (input.WasJustPressed(BUTTON.FIRE_SECONDARY)) RunLocalScan(player.IPlayer);
        }

        private void cmdQCounter(IPlayer iplayer, string command, string[] args)
        {
            if (!permission.UserHasPermission(iplayer.Id, permHeld)) { Message(iplayer, "notauthorized"); return; }
            BasePlayer player = iplayer.Object as BasePlayer;

            if (issuedCounters.ContainsKey(player.userID))
            {
                Message(iplayer, "");
                return;
            }

            ItemContainer backpack = null;
            if (player.inventory.containerBelt.FindItemsByItemID(999690781).FirstOrDefault() != null)
            {
                Message(iplayer, "alreadyin", Lang("belt"));
                return;
            }
            if (player.inventory.containerMain.FindItemsByItemID(999690781).FirstOrDefault() != null)
            // Play effect on exit portal, since both entranc
            {
                Message(iplayer, "alreadyin", Lang("main"));
                return;
            }
            if (Backpacks)
            {
                backpack = Backpacks?.Call("API_GetBackpackContainer", ulong.Parse(iplayer.Id)) as ItemContainer;
                if (backpack != null)
                {
                    for (int i = 0; i < backpack.itemList.Count; i++)
                    {
                        if (backpack.itemList[i].info.itemid == 999690781)
                        {
                            Message(iplayer, "alreadyin", Lang("backpack"));
                            return;
                        }
                    }
                }
            }

            if (!player.inventory.containerBelt.IsFull())
            {
                Item item = ItemManager.CreateByItemID(999690781, 1, 0);
                item.MoveToContainer(player.inventory.containerBelt);
                issuedCounters.Add(player.userID, (uint)item.uid.Value);
                player.inventory.containerBelt.MarkDirty();
                Message(iplayer, "addedto", Lang("belt"));
                return;
            }
            if (!player.inventory.containerMain.IsFull())
            {
                Item item = ItemManager.CreateByItemID(999690781, 1, 0);
                item.MoveToContainer(player.inventory.containerMain);
                issuedCounters.Add(player.userID, (uint)item.uid.Value);
                player.inventory.containerMain.MarkDirty();
                Message(iplayer, "addedto", Lang("main"));
                return;
            }
            if (backpack?.IsFull() == false)
            {
                Item item = ItemManager.CreateByItemID(999690781, 1, 0);
                item.MoveToContainer(backpack);
                issuedCounters.Add(player.userID, (uint)item.uid.Value);
                backpack.MarkDirty();
                Message(iplayer, "addedto", Lang("backpack"));
                return;
            }
            Message(iplayer, "noroom");
        }

        private void cmdQRadar(IPlayer iplayer, string command, string[] args)
        {
            if (!permission.UserHasPermission(iplayer.Id, permUse)) { Message(iplayer, "notauthorized"); return; }
            RunAreaScan(iplayer);
        }

        private void RunLocalScan(IPlayer iplayer)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            BaseEntity target = RaycastAll<BaseEntity>(player.eyes.HeadRay()) as BaseEntity;
            if (target?.GetBuildingPrivilege() == null)
            {
                string nom = target?.GetType().Name;
                if (string.IsNullOrEmpty(nom)) return;
                Message(iplayer, $"[{Name}] {nom}:{target?.ShortPrefabName}");
            }
        }

        private void RunAreaScan(IPlayer iplayer, bool gc = false)
        {
            BasePlayer player = iplayer.Object as BasePlayer;
            if (playerUse.Contains(player.userID) && !gc) { Message(iplayer, "tooquick", configData.frequency.ToString()); return; }

            if (configData.playSound) Effect.server.Run("assets/bundled/prefabs/fx/invite_notice.prefab", player.transform.position);

            foreach (RaycastHit hit in Physics.SphereCastAll(player.eyes.position, configData.range, Vector3.forward, configData.range))
            {
                BaseEntity ent = hit.GetEntity();
                if (ent != null)
                {
                    string entName = ent?.ShortPrefabName;
                    if (ent.GetBuildingPrivilege() != null) continue;
                    //if (ent.GetBuildingPrivilege() != null && configData.showEntiesInPrivilegeRange)
                    //{
                    //    bool found = false;
                    //    BuildingPrivlidge bp = ent.GetBuildingPrivilege();
                    //    foreach (ProtoBuf.PlayerNameID p in bp.authorizedPlayers.ToArray())
                    //    {
                    //        if (p.userid == player.userID)
                    //        {
                    //            found = true;
                    //        }
                    //    }
                    //    if (!found || IsFriend(player.userID, ent.OwnerID))
                    //    {
                    //        continue;
                    //    }
                    //}

                    if (ent is BushEntity || ent is TreeEntity) continue;

                    if (ent is BuildingBlock)
                    {
                        player?.SendConsoleCommand("ddraw.text", configData.duration, Color.blue, ent.transform.position, $"<size=20>{entName}</size>");
                        continue;
                    }
                    else if (ent is BaseVehicle)
                    {
                        player?.SendConsoleCommand("ddraw.text", configData.duration, Color.cyan, ent.transform.position, $"<size=20>{entName}</size>");
                        continue;
                    }
                    else if (ent is OreResourceEntity)
                    {
                        player?.SendConsoleCommand("ddraw.text", configData.duration, Color.white, ent.transform.position, $"<size=20>{entName}</size>");
                        continue;
                    }
                    else if (ent is ResourceEntity)
                    {
                        player?.SendConsoleCommand("ddraw.text", configData.duration, Color.gray, ent.transform.position, $"<size=20>{entName}</size>");
                        continue;
                    }
                    else if (ent is LootContainer)
                    {
                        player?.SendConsoleCommand("ddraw.text", configData.duration, Color.magenta, ent.transform.position, $"<size=20>{entName}</size>");
                        continue;
                    }
                    else if (ent is ScientistNPC && permission.UserHasPermission(player.UserIDString, permSeeNPC))
                    {
                        player?.SendConsoleCommand("ddraw.text", configData.duration, Color.red, ent.transform.position, $"<size=20>{entName}</size>");
                    }
                    else if (ent is BasePlayer || ent is BaseAnimalNPC)
                    {
                        if (ent is BasePlayer)
                        {
                            if (!iplayer.IsAdmin && !configData.showPlayersForAll) continue;
                            else if (iplayer.IsAdmin && !configData.showPlayersForAdmin) continue;
                        }
                        player?.SendConsoleCommand("ddraw.text", configData.duration, Color.red, ent.transform.position, $"<size=20>{entName}</size>");
                        continue;
                    }
                    player?.SendConsoleCommand("ddraw.text", configData.duration, Color.green, ent.transform.position, $"<size=20>{entName}</size>");
                }
            }
            playerUse.Add(player.userID);
            timer.Once(configData.frequency, () => playerUse.Remove(player.userID));
        }

        private object RaycastAll<T>(Ray ray) where T : BaseEntity
        {
            RaycastHit[] hits = Physics.RaycastAll(ray);
            GamePhysics.Sort(hits);
            const float distance = 100f;
            object target = false;
            foreach (RaycastHit hit in hits)
            {
                BaseEntity ent = hit.GetEntity();
                if (ent is T && hit.distance < distance)
                {
                    target = ent;
                    break;
                }
            }

            return target;
        }

        private class ConfigData
        {
            public bool playSound;
            public float range;
            public float duration;
            public float frequency;
            public bool showPlayersForAdmin;
            public bool showPlayersForAll;
            public bool specialHandlingForGC;

            //public bool showEntiesInPrivilegeRange;

            //public bool useFriends;
            //public bool useClans;
            //public bool useTeams;

            public VersionNumber Version;
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < new VersionNumber(1, 0, 3))
            {
                configData.specialHandlingForGC = true;
            }

            configData.Version = Version;
            SaveConfig(configData);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            ConfigData config = new()
            {
                playSound = true,
                range = 50f,
                duration = 10f,
                frequency = 20f,
                showPlayersForAdmin = true,
                showPlayersForAll = false,
                specialHandlingForGC = true
            };

            SaveConfig(config);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        //private void LoadData()
        //{
        //    issuedCounters = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, uint>>(Name + "/issued");
        //}

        //private void SaveData()
        //{
        //    Interface.GetMod().DataFileSystem.WriteObject(Name + "/issued", issuedCounters);
        //}
    }
}
