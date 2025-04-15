using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TownOfHostForE.Roles.Core;
using static UnityEngine.GraphicsBuffer;
using UnityEngine;
using TownOfHostForE.Roles.Neutral;
using TownOfHostForE.Roles;
using TownOfHostForE.Attributes;
using static TownOfHostForE.Options;

namespace TownOfHostForE.Modules
{
    public static class LoversManager
    {
        //ラバーズMaster
        public static Dictionary<byte, LoversTeam> LoversMaster = new ();

        public static OptionItem OptionMakeLoversPair;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(73000, TabGroup.Addons, CustomRoles.Lovers, 2);
            OptionMakeLoversPair = IntegerOptionItem.Create(73010, "MakeLoversPair", new(1, 7, 1), 1, TabGroup.Addons, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lovers]);
            LoversAddWin = BooleanOptionItem.Create(73020, "LoversAddWin", false, TabGroup.Addons, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lovers]);
        }
        /// <summary>
        /// ラバーズ用チェックマーダー
        /// 所属させる奴
        /// </summary>
        /// <param name="killer"></param>
        /// <param name="target"></param>
        public static void CheckMurderLovers(PlayerControl killer,PlayerControl target)
        {
            LoversTeam tempTeam = null;
            if (LoversMaster.Count == 0 || !LoversMaster.ContainsKey(killer.PlayerId))
            {
                tempTeam = new LoversTeam();
                tempTeam.LeaderId = killer.PlayerId;
                tempTeam.Teams.Add(killer.PlayerId);
                LoversMaster.Add(killer.PlayerId,tempTeam);
                killer.RpcSetCustomRole(CustomRoles.Lovers);
            }
            else
            {
                tempTeam = LoversMaster[killer.PlayerId];
            }

            //相手が姫ちゃん or 純愛者なら残弾を失くす。
            var targetRoleClass = target.GetRoleClass();
            if (targetRoleClass is PlatonicLover platonicLover)
            {
                platonicLover.isMadeLover = true;
            }
            else if (targetRoleClass is OtakuPrincess princess)
            {
                princess.princeMax = 0;
            }

            //相手が他のラバーズリーダーか
            if (LoversMaster.ContainsKey(target.PlayerId))
            {
                //相手のチームを配下に
                tempTeam.Teams.AddRange(LoversMaster[target.PlayerId].Teams);
                //相手のマスター削除
                LoversMaster.Remove(target.PlayerId);

            }
            else
            {
                (bool checkResult, LoversTeam team) = CheckOtherLovers(target.PlayerId);

                //他ラバーズ所属済み
                if (checkResult)
                {
                    team.Teams.Remove(target.PlayerId);
                }

                tempTeam.Teams.Add(target.PlayerId);
            }

            LoversMaster[killer.PlayerId] = tempTeam;

            target.RpcSetCustomRole(CustomRoles.Lovers);
            killer.RpcProtectedMurderPlayer(target);
            target.RpcProtectedMurderPlayer(target);
            Logger.Info($"{killer.GetNameWithRole()} : 恋人を作った", killer.GetCustomRole().ToString());

            RPC.SyncLoversPlayers();
            killer.ResetKillCooldown();
            Utils.NotifyRoles();
        }

        public static void KillLovers(byte targetId)
        {
            (bool checkResult, LoversTeam team) = CheckOtherLovers(targetId);

            if (!checkResult) return;

            if (team.IsDeath) return;

            team.IsDeath = true;
            foreach (var pcid in team.Teams)
            {
                var pc = Utils.GetPlayerById(pcid);
                pc.RpcMurderPlayer(pc);
            }
        }

        public static void ExileLovers(byte targetId)
        {
            (bool checkResult, LoversTeam team) = CheckOtherLovers(targetId);

            if (!checkResult) return;

            if (team.IsDeath) return;

            team.IsDeath = true;
            foreach (var pcid in team.Teams)
            {
                var pc = Utils.GetPlayerById(pcid);
                pc.RpcExile();
            }
        }

        public static Color GetLeaderColor(byte pcid)
        {
            //メガんたって何の色か分からないから初期値にする
            Color tempColor = Color.magenta;

            (bool checkResult, LoversTeam team) = CheckOtherLovers(pcid);
            if (checkResult)
            {
                var pcInfo = Utils.GetPlayerInfoById(team.LeaderId);
                tempColor = pcInfo.Color;
            }

            return tempColor;
        }

        private static (bool,LoversTeam?) CheckOtherLovers(byte targetId)
        {
            //リーダーか?
            if (LoversMaster.ContainsKey(targetId))
            {
                return (true, LoversMaster[targetId]);
            }

            //リーダーじゃない
            foreach (var team in LoversMaster.Values)
            {
                if (team.Teams == null)
                {
                    continue;
                }
                if (team.Teams.Contains(targetId))
                {
                    return (true,team);
                }
            }

            return (false,null);
        }


        public static bool CheckLoversWin(GameOverReason reason, ref List<PlayerControl> winnerLoversList)
        {
            winnerLoversList = new();
            //ラバーズがいないなら処理しない
            if (LoversMaster == null || LoversMaster.Count() == 0) return false;

            //タスク勝利は奪えない
            if (reason == GameOverReason.CrewmatesByTask) return false;

            byte winnerTeamLeaderId = CheckWinnerLoversLeaderID();

            //ラバーズの生き残りがいない場合 もしくは何故か取得したリーダーが登録されていない場合
            if (winnerTeamLeaderId == byte.MaxValue || !LoversMaster.ContainsKey(winnerTeamLeaderId)) return false;

            //生き残りの1チームなんで選択
            foreach (var id in LoversMaster[winnerTeamLeaderId].Teams)
            {
                winnerLoversList.Add(Utils.GetPlayerById(id));
            }

            //そのチームがラバーズ若しくは純愛者で、追加勝利ありだとここでは処理しない。
            if (Utils.GetPlayerById(winnerTeamLeaderId).GetCustomRole() != CustomRoles.OtakuPrincess
                &&
                ((Utils.GetPlayerById(winnerTeamLeaderId).GetCustomRole() == CustomRoles.PlatonicLover &&
                 PlatonicLover.AddWin)
                 ||
                 (Utils.GetPlayerById(winnerTeamLeaderId).GetCustomSubRoles().All(p => p != CustomRoles.Lovers) == false &&
                 Options.LoversAddWin.GetBool()))
                )
            {
                return false;
            }

            return true;
        }

        public static bool CheckLoversAddWin(PlayerControl pc, List<PlayerControl> winnerLoversList)
        {
            //死んでるのは対象外
            if (!pc.IsAlive()) return false;
            //対象がいないなら対象外
            if (winnerLoversList.Any(p => p.PlayerId == pc.PlayerId) == false) return false;

            //欲しいのは代表のロール
            var cRole = winnerLoversList[0].GetCustomRole();
            switch (cRole)
            {
                case CustomRoles.PlatonicLover:
                    return PlatonicLover.AddWin;
                case CustomRoles.Lovers:
                default:
                    //代表のロールが純愛者以外であればラバーズの追加勝利から取得
                    //(姫ちゃんには追加はないので)
                    return Options.LoversAddWin.GetBool();
            }
        }

        public static bool CheckMyLovers(byte seerId, byte targetId)
        {
            foreach (var list in LoversMaster)
            {
                if (list.Value.Teams == null)
                {
                    continue;
                }
                if (list.Value.Teams.Contains(seerId) && list.Value.Teams.Contains(targetId))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool CheckLoversSuicide(byte playerId)
        {
            bool result = false;

            //ラバーズ系がいない
            if (!(CustomRoles.Lovers.IsPresent() || CustomRoles.PlatonicLover.IsPresent() || CustomRoles.OtakuPrincess.IsPresent()))
                return result;

            foreach(var data in LoversMaster)
            {
                if (data.Value.Teams == null)
                {
                    continue;
                }
                //このチームには所属していない場合
                if (!data.Value.Teams.Contains(playerId))
                {
                    continue;
                }

                //所属している場合チームが吊られる
                result = true;
                break;
            }

            return result;
        }

        public static List<PlayerControl> GetLoversList(PlayerControl pc)
        {
            foreach (var data in LoversMaster)
            {
                if (data.Value.Teams == null)
                {
                    continue;
                }
                //所属済みか判定
                if (!data.Value.Teams.Contains(pc.PlayerId))
                {
                    continue;
                }

                //所属済み

                List<PlayerControl> lovers = new();

                foreach (var id in data.Value.Teams)
                {
                    lovers.Add(Utils.GetPlayerById(id));
                }
                return lovers;
            }

            return null;
        }

        public static bool CheckOtherLovers(byte targetId, out byte leader)
        {
            leader = byte.MaxValue;

            foreach (var data in LoversMaster)
            {
                if (data.Value.Teams == null)
                {
                    continue;
                }
                if (data.Value.Teams.Contains(targetId))
                {
                    leader = data.Value.LeaderId;
                    return true;
                }
            }

            return false;
        }

        public static void LoversSuicide(byte deathId = 0x7f, bool isExiled = false)
        {
            if (CheckLoversSuicide(deathId, out List<PlayerControl> LoversList))
            {
                foreach (var loversPlayer in LoversList)
                {
                    //生きていて死ぬ予定でなければスキップ
                    if (!loversPlayer.Data.IsDead && loversPlayer.PlayerId != deathId) continue;

                    foreach (var partnerPlayer in LoversList)
                    {
                        //本人ならスキップ
                        if (loversPlayer.PlayerId == partnerPlayer.PlayerId) continue;

                        //残った恋人を全て殺す(2人以上可)
                        //生きていて死ぬ予定もない場合は心中
                        if (partnerPlayer.PlayerId != deathId && !partnerPlayer.Data.IsDead)
                        {
                            PlayerState.GetByPlayerId(partnerPlayer.PlayerId).DeathReason = CustomDeathReason.FollowingSuicide;
                            if (isExiled)
                                MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.FollowingSuicide, partnerPlayer.PlayerId);
                            else
                                partnerPlayer.RpcMurderPlayer(partnerPlayer);
                        }
                    }
                }
            }
        }

        public static int GetAssignLoversCount()
        {
            return OptionMakeLoversPair.GetInt() * 2;
        }
        public static CustomRoles[] SetAssignLovers()
        {
            List<CustomRoles> assignInfo = new();

            for (int i = 0; i < OptionMakeLoversPair.GetInt() * 2; i++)
            {
                assignInfo.Add(CustomRoles.Lovers);
            }
            return assignInfo.ToArray();
        }

        private static bool CheckLoversSuicide(byte deathId, out List<PlayerControl> loversList)
        {
            //RoleAssignManager.checkAssingList();
            loversList = null;
            //ラバーズ系がいないなら処理しない
            if (!(CustomRoles.Lovers.IsPresent() ||
                 CustomRoles.PlatonicLover.IsPresent() ||
                 CustomRoles.OtakuPrincess.IsPresent()))
                return false;

            //死者決定してないなら処理しない
            if (deathId == 0x7f) return false;

            bool result = false;

            foreach (var data in LoversMaster)
            {
                //チーム無しは関係なし
                if (data.Value.Teams == null)
                {
                    continue;
                }

                //このチームには所属していない場合
                if (!data.Value.Teams.Contains(deathId))
                {
                    continue;
                }

                //死んでるなら対象外
                if (data.Value.IsDeath)
                {
                    continue;
                }

                loversList = new();

                //所属しているチームを引数に設定
                foreach (var id in data.Value.Teams)
                {
                    loversList.Add(Utils.GetPlayerById(id));
                }

                //所属しているチームは死にました。
                data.Value.IsDeath = true;
                result = data.Value.IsDeath;
                break;
            }

            return result;
        }

        private static byte CheckWinnerLoversLeaderID()
        {
            Dictionary<byte, int> countLovers = new();

            foreach (var lvData in LoversMaster.Values)
            {
                //生存しているか確認
                if (lvData.IsDeath)
                {
                    //死亡はカウント外
                    continue;
                }
                if (lvData.Teams == null)
                {
                    continue;
                }

                //生存人数を記録
                countLovers.Add(lvData.LeaderId,lvData.Teams.Count());
            }

            int maxCount = -1;
            byte Leader = byte.MaxValue;
            List<byte> drrowCount = new();
            //生き残り精査
            foreach (var data in countLovers)
            {
                //相手の人数より多い場合
                if (data.Value > maxCount)
                {
                    maxCount = data.Value;
                    Leader = data.Key;
                }
                //相手の人数と等しい場合
                else if (data.Value == maxCount)
                {
                    drrowCount.Add(data.Key);
                    if (!drrowCount.Contains(Leader)) drrowCount.Add(Leader);
                }
            }

            //最終確認
            //同じ数のラバーズが勝利条件を満たしているとき
            if (drrowCount.Count() > 0)
            {
                //一番最初に登録されてる奴が勝者

                //なんか0指定で一番最後のラバーズが取れるから取り敢えず一番最後のラバーズを取る
                Leader = drrowCount[drrowCount.Count() - 1];
            }

            return Leader;
        }
    }

    public class LoversTeam
    {
        //コンストラクタ
        public LoversTeam()
        {
            _leaderId = byte.MaxValue;
            _teams = new List<byte>();
            _isDeath = false;
        }


        private byte _leaderId = byte.MaxValue;

        private List<byte> _teams;

        private bool _isDeath = false;

        public byte LeaderId
        {
            get { return _leaderId; }
            set { _leaderId = value; }
        }

        public List<byte> Teams
        {
            get { return this._teams; }
            set { this._teams = value; }
        }

        public bool IsDeath
        {
            get { return this._isDeath; }
            set { this._isDeath = value; }
        }
    }

}
