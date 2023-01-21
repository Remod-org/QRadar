#region License (GPL v2)
/*
    DESCRIPTION
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
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("QRadar", "RFC1920", "1.0.1")]
    [Description("Simple player radar for world object")]
    internal class QRadar : RustPlugin
    {
        private ConfigData configData;
        private const string permUse = "qradar.use";
        private List<ulong> playerUse = new List<ulong>();
        [PluginReference]
        private readonly Plugin Friends, Clans;

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        private void LMessage(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["tooquick"] = "You must wait {0} seconds between radar bursts.",
                ["notauthorized"] = "You don't have permission to do that !!"
            }, this);
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permUse, this);
            LoadConfigVariables();
            AddCovalenceCommand("qradar", "cmdQRadar");
        }

        private void cmdQRadar(IPlayer iplayer, string command, string[] args)
        {
            if (!permission.UserHasPermission(iplayer.Id, permUse)) { Message(iplayer, "notauthorized"); return; }

            BasePlayer player = iplayer.Object as BasePlayer;
            if (playerUse.Contains(player.userID)) { Message(iplayer, "tooquick", configData.frequency.ToString()); return; }

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
                    else if (ent is ResourceEntity && !(ent is OreResourceEntity))
                    {
                        player?.SendConsoleCommand("ddraw.text", configData.duration, Color.gray, ent.transform.position, $"<size=20>{entName}</size>");
                        continue;
                    }
                    else if (ent is OreResourceEntity)
                    {
                        player?.SendConsoleCommand("ddraw.text", configData.duration, Color.white, ent.transform.position, $"<size=20>{entName}</size>");
                        continue;
                    }
                    else if (ent is LootContainer)
                    {
                        player?.SendConsoleCommand("ddraw.text", configData.duration, Color.magenta, ent.transform.position, $"<size=20>{entName}</size>");
                        continue;
                    }
                    else if (ent is BasePlayer || ent is BaseAnimalNPC)
                    {
                        player?.SendConsoleCommand("ddraw.text", configData.duration, Color.red, ent.transform.position, $"<size=20>{entName}</size>");
                        continue;
                    }
                    player?.SendConsoleCommand("ddraw.text", configData.duration, Color.green, ent.transform.position, $"<size=20>{entName}</size>");
                }
            }
            playerUse.Add(player.userID);
            timer.Once(configData.frequency, () => playerUse.Remove(player.userID));
        }

        private class ConfigData
        {
            public bool playSound;
            public float range;
            public float duration;
            public float frequency;

            //public bool showEntiesInPrivilegeRange;

            //public bool useFriends;
            //public bool useClans;
            //public bool useTeams;

            public VersionNumber Version;
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            configData.Version = Version;
            SaveConfig(configData);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            ConfigData config = new ConfigData()
            {
                playSound = true,
                range = 50f,
                duration = 10f,
                frequency = 20f
            };

            SaveConfig(config);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
    }
}
