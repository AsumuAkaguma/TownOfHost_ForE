using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using UnityEngine;

using TownOfHostForE.Roles.Core;
using TownOfHostForE.Roles.Core.Interfaces;
using TownOfHostForE.Roles.Crewmate;
using TownOfHostForE.Roles.Neutral;
using TownOfHostForE.Roles.Animals;
using TownOfHostForE.Patches;
using TownOfHostForE.Modules;

namespace TownOfHostForE
{
    [HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
    class GameEndChecker
    {
        private static GameEndPredicate predicate;
        public static bool Prefix()
        {
            if (!AmongUsClient.Instance.AmHost) return true;

            //ゲーム終了判定済みなら中断
            if (predicate == null) return false;

            //ゲーム終了しないモードで廃村以外の場合は中断
            if (Options.NoGameEnd.GetBool() && CustomWinnerHolder.WinnerTeam != CustomWinner.Draw) return false;

            //廃村用に初期値を設定
            var reason = GameOverReason.ImpostorsByKill;

            //ゲーム終了判定
            predicate.CheckForEndGame(out reason);

            //ゲーム終了時
            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default)
            {
                //カモフラージュ強制解除
                Main.AllPlayerControls.Do(pc => Camouflage.RpcSetSkin(pc, ForceRevert: true, RevertToDefault: true));

                switch (CustomWinnerHolder.WinnerTeam)
                {
                    case CustomWinner.Crewmate:
                        foreach (var pc in PlayerControl.AllPlayerControls)
                        {
                            if(pc.Is(CustomRoleTypes.Crewmate) && !pc.Is(CustomRoles.Lovers)
                                && !(pc.Is(CustomRoles.Bakery) && Bakery.IsNeutral(pc)) && !pc.Is(CustomRoles.Archenemy))
                            {
                                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            }
                        }
                        break;
                    case CustomWinner.Impostor:
                        if (Egoist.CheckWin()) break;

                        Main.AllPlayerControls
                            .Where(pc => (pc.Is(CustomRoleTypes.Impostor) || pc.Is(CustomRoleTypes.Madmate)) && !pc.Is(CustomRoles.Lovers) && !pc.Is(CustomRoles.Archenemy))
                            .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                        break;
                }
                if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw and not CustomWinner.None)
                {
                    List<PlayerControl> winnerLoversList = null;
                    if(LoversManager.CheckLoversWin(reason,ref winnerLoversList))
                    {
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Lovers);
                        //Main.AllPlayerControls
                        winnerLoversList
                            .Where(p => p.Is(CustomRoles.Lovers) && p.IsAlive())
                            .Do(p => CustomWinnerHolder.WinnerIds.Add(p.PlayerId));
                    }
                    foreach (var pc in PlayerControl.AllPlayerControls)
                    {
                        if (pc.Is(CustomRoles.DarkHide) && !pc.Data.IsDead
                            && ((CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor && !reason.Equals(GameOverReason.ImpostorsBySabotage)) ||
                                 CustomWinnerHolder.WinnerTeam == CustomWinner.DarkHide
                                ||
                                (CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate && !reason.Equals(GameOverReason.CrewmatesByTask) && ((DarkHide)pc.GetRoleClass()).IsWinKill == true)))
                        {
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.DarkHide);
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        }
                        else if (pc.Is(CustomRoles.Bakery) && Bakery.IsNeutral(pc) && pc.IsAlive()
                            && ((CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor && !reason.Equals(GameOverReason.ImpostorsBySabotage)) || CustomWinnerHolder.WinnerTeam == CustomWinner.NBakery
                            || (CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate && !reason.Equals(GameOverReason.CrewmatesByTask))))
                        {
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.NBakery);
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        }
                        else if (pc.Is(CustomRoles.Tuna) && pc.IsAlive())
                        {
                            //マグロ側で処理
                            Tuna.CheckAliveWin(pc);
                        }
                    }

                    List<PlayerControl> shPC = new ();
                    //追加勝利陣営
                    foreach (var pc in Main.AllPlayerControls)
                    {
                        //Lover追加勝利
                        if(LoversManager.CheckLoversAddWin(pc,winnerLoversList))
                        {
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerRoles.Add(CustomRoles.Lovers);
                        }
                        //シュレ猫は後でもう一度
                        if (pc.GetCustomRole() == CustomRoles.SchrodingerCat)
                        {
                            shPC.Add(pc);
                        }
                        else if (pc.GetRoleClass() is IAdditionalWinner additionalWinner)
                        {
                            var winnerRole = pc.GetCustomRole();
                            //bool result = additionalWinner.CheckWin(out var winnerType);
                            bool result = additionalWinner.CheckWin(ref winnerRole);
                            if (result)
                            {
                                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                                CustomWinnerHolder.AdditionalWinnerRoles.Add(winnerRole);
                            }
                        }
                        if (Duelist.ArchenemyCheckWin(pc))
                        {
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerRoles.Add(CustomRoles.Archenemy);
                        }
                    }
                    //弁護士且つ追跡者
                    Lawyer.EndGameCheck();
                    //シュレ猫用
                    foreach (var sh in shPC)
                    {
                        if (sh.GetRoleClass() is IAdditionalWinner additionalWinner)
                        {
                            var winnerRole = sh.GetCustomRole();
                            bool result = additionalWinner.CheckWin(ref winnerRole);
                            if (result)
                            {
                                CustomWinnerHolder.WinnerIds.Add(sh.PlayerId);
                                CustomWinnerHolder.AdditionalWinnerRoles.Add(CustomRoles.SchrodingerCat);
                            }
                        }
                    }
                }
                ShipStatus.Instance.enabled = false;
                StartEndGame(reason);
                predicate = null;
            }
            return false;
        }

        public static void StartEndGame(GameOverReason reason)
        {
            AmongUsClient.Instance.StartCoroutine(CoEndGame(AmongUsClient.Instance, reason).WrapToIl2Cpp());
        }

        private static IEnumerator CoEndGame(AmongUsClient self, GameOverReason reason)
        {
            //ペットを強制的につけた人は外す
            PetSettings.RemovePetSet();


            // サーバー側のパケットサイズ制限によりCustomRpcSenderが利用できないため，遅延を挟むことで順番の整合性を保つ．

            //ゴーストロール化
            // バニラ画面でのアウトロを正しくするためのゴーストロール化
            List<byte> ReviveRequiredPlayerIds = new();
            var winner = CustomWinnerHolder.WinnerTeam;
            foreach (var pc in Main.AllPlayerControls)
            {
                if (winner == CustomWinner.Draw)
                {
                    SetGhostRole(ToGhostImpostor: true);
                    continue;
                }
                bool canWin = CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId) ||
                        CustomWinnerHolder.WinnerRoles.Contains(pc.GetCustomRole());
                bool isCrewmateWin = reason.Equals(GameOverReason.CrewmatesByVote) || reason.Equals(GameOverReason.CrewmatesByTask);
                SetGhostRole(ToGhostImpostor: canWin ^ isCrewmateWin);

                void SetGhostRole(bool ToGhostImpostor)
                {
                    var isDead = pc.Data.IsDead;
                    if (!isDead) ReviveRequiredPlayerIds.Add(pc.PlayerId);
                    if (ToGhostImpostor)
                    {
                        Logger.Info($"{pc.GetNameWithRole()}: ImpostorGhostに変更", "ResetRoleAndEndGame");
                        pc.RpcSetRole(RoleTypes.ImpostorGhost);
                    }
                    else
                    {
                        Logger.Info($"{pc.GetNameWithRole()}: CrewmateGhostに変更", "ResetRoleAndEndGame");
                        pc.RpcSetRole(RoleTypes.CrewmateGhost);
                    }
                    // 蘇生までの遅延の間にオートミュートをかけられないように元に戻しておく
                    pc.Data.IsDead = isDead;
                }
            }

            // CustomWinnerHolderの情報の同期
            var winnerWriter = self.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.EndGame, SendOption.Reliable);
            CustomWinnerHolder.WriteTo(winnerWriter);
            self.FinishRpcImmediately(winnerWriter);

            // 蘇生を確実にゴーストロール設定の後に届けるための遅延
            yield return new WaitForSeconds(EndGameDelay);

            if (ReviveRequiredPlayerIds.Count > 0)
            {
                // 蘇生 パケットが膨れ上がって死ぬのを防ぐため，1送信につき1人ずつ蘇生する
                for (int i = 0; i < ReviveRequiredPlayerIds.Count; i++)
                {
                    var playerId = ReviveRequiredPlayerIds[i];
                    var playerInfo = GameData.Instance.GetPlayerById(playerId);
                    // 蘇生
                    playerInfo.IsDead = false;
                    // 送信
                    playerInfo.MarkDirty();
                    AmongUsClient.Instance.SendAllStreamedObjects();
                }
                // ゲーム終了を確実に最後に届けるための遅延
                yield return new WaitForSeconds(EndGameDelay);
            }
            // ゲーム終了
            GameManager.Instance.RpcEndGame(reason, false);
        }
        private const float EndGameDelay = 0.2f;

        public static void SetPredicateToNormal() => predicate = new NormalGameEndPredicate();
        public static void SetPredicateToHideAndSeek() => predicate = new HideAndSeekGameEndPredicate();
        public static void SetPredicateToSuperBombParty() => predicate = new SuperBombPartyGameEndPredicate();

        // ===== ゲーム終了条件 =====
        // 通常ゲーム用
        class NormalGameEndPredicate : GameEndPredicate
        {
            public override bool CheckForEndGame(out GameOverReason reason)
            {
                reason = GameOverReason.ImpostorsByKill;
                if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return false;
                if (CheckGameEndByLivingPlayers(out reason)) return true;
                if (CheckGameEndByTask(out reason)) return true;
                if (CheckGameEndBySabotage(out reason)) return true;

                return false;
            }
            public bool CheckGameEndByLivingPlayers(out GameOverReason reason)
            {
                reason = GameOverReason.ImpostorsByKill;

                int Imp = Utils.AlivePlayersCount(CountTypes.Impostor);
                int Jackal = Utils.AlivePlayersCount(CountTypes.Jackal);
                int Animals = Utils.AlivePlayersCount(CountTypes.Animals);
                int Crew = Utils.AlivePlayersCount(CountTypes.Crew);

                if (Imp == 0 && Crew == 0 && Jackal == 0 && Animals == 0) //全滅
                {
                    reason = GameOverReason.ImpostorsByKill;
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
                }
                else if (Main.AllAlivePlayerControls.All(p => p.Is(CustomRoles.Lovers))) //ラバーズ勝利
                {
                    reason = GameOverReason.ImpostorsByKill;
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Lovers);
                }
                else if (Jackal == 0 && Animals == 0 && Crew <= Imp) //インポスター勝利
                {
                    reason = GameOverReason.ImpostorsByKill;
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Impostor);
                }
                else if (Imp == 0 && Animals == 0 && Crew <= Jackal) //ジャッカル勝利
                {
                    reason = GameOverReason.ImpostorsByKill;
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jackal);
                    CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Jackal);
                    CustomWinnerHolder.WinnerRoles.Add(CustomRoles.JClient);
                }
                else if (Imp == 0 && Jackal == 0 && Crew <= Animals) //アニマルズ勝利
                {
                    reason = GameOverReason.ImpostorsByKill;
                    Vulture.AnimalsWin();
                }
                else if (Jackal == 0 && Imp == 0 && Animals == 0) //クルー勝利
                {
                    reason = GameOverReason.CrewmatesByVote;
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Crewmate);
                }
                else return false; //勝利条件未達成

                return true;
            }
        }

        // HideAndSeek用
        class HideAndSeekGameEndPredicate : GameEndPredicate
        {
            public override bool CheckForEndGame(out GameOverReason reason)
            {
                reason = GameOverReason.ImpostorsByKill;
                if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return false;

                if (CheckGameEndByLivingPlayers(out reason)) return true;
                if (CheckGameEndByTask(out reason)) return true;

                return false;
            }

            public bool CheckGameEndByLivingPlayers(out GameOverReason reason)
            {
                reason = GameOverReason.ImpostorsByKill;

                int Imp = Utils.AlivePlayersCount(CountTypes.Impostor);
                int Crew = Utils.AlivePlayersCount(CountTypes.Crew);

                if (Imp == 0 && Crew == 0) //全滅
                {
                    reason = GameOverReason.ImpostorsByKill;
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
                }
                else if (Crew <= 0) //インポスター勝利
                {
                    reason = GameOverReason.ImpostorsByKill;
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Impostor);
                }
                else if (Imp == 0) //クルー勝利(インポスター切断など)
                {
                    reason = GameOverReason.CrewmatesByVote;
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Crewmate);
                }
                else return false; //勝利条件未達成

                return true;
            }
        }

        // 大惨事爆裂大戦用
        class SuperBombPartyGameEndPredicate : GameEndPredicate
        {
            public override bool CheckForEndGame(out GameOverReason reason)
            {
                reason = GameOverReason.ImpostorsByKill;
                if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return false;

                if (CheckGameEndByLivingPlayers(out reason)) return true;

                return false;
            }

            public bool CheckGameEndByLivingPlayers(out GameOverReason reason)
            {
                reason = GameOverReason.ImpostorsByKill;

                int AliveSB = Utils.AlivePlayersCount(CountTypes.SB);

                if (AliveSB == 0) //全滅
                {
                    reason = GameOverReason.ImpostorsByKill;
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
                }
                else if (AliveSB == 1) //勝者決定
                {
                    reason = GameOverReason.ImpostorsByKill;
                    foreach (var AlivePlayer in Main.AllAlivePlayerControls)
                    {
                        CustomWinnerHolder.WinnerIds.Add(AlivePlayer.PlayerId);
                    }
                }
                else return false; //勝利条件未達成

                return true;
            }
        }
    }

    public abstract class GameEndPredicate
    {
        /// <summary>ゲームの終了条件をチェックし、CustomWinnerHolderに値を格納します。</summary>
        /// <params name="reason">バニラのゲーム終了処理に使用するGameOverReason</params>
        /// <returns>ゲーム終了の条件を満たしているかどうか</returns>
        public abstract bool CheckForEndGame(out GameOverReason reason);

        /// <summary>GameData.TotalTasksとCompletedTasksをもとにタスク勝利が可能かを判定します。</summary>
        public virtual bool CheckGameEndByTask(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            if (Options.DisableTaskWin.GetBool() || TaskState.InitialTotalTasks == 0) return false;
            //ラバーズ変化などでトータルタスクが0になった際はゲームを終えない
            if (GameData.Instance.TotalTasks == 0) return false;

            if (GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks)
            {
                reason = GameOverReason.CrewmatesByTask;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Crewmate);
                return true;
            }
            return false;
        }
        /// <summary>ShipStatus.Systems内の要素をもとにサボタージュ勝利が可能かを判定します。</summary>
        public virtual bool CheckGameEndBySabotage(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            if (ShipStatus.Instance.Systems == null) return false;

            // TryGetValueは使用不可
            var systems = ShipStatus.Instance.Systems;
            LifeSuppSystemType LifeSupp;
            if (systems.ContainsKey(SystemTypes.LifeSupp) && // サボタージュ存在確認
                (LifeSupp = systems[SystemTypes.LifeSupp].TryCast<LifeSuppSystemType>()) != null && // キャスト可能確認
                LifeSupp.Countdown < 0f) // タイムアップ確認
            {
                // 酸素サボタージュ
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Impostor);
                reason = GameOverReason.ImpostorsBySabotage;
                LifeSupp.Countdown = 10000f;
                return true;
            }

            ISystemType sys = null;
            if (systems.ContainsKey(SystemTypes.Reactor)) sys = systems[SystemTypes.Reactor];
            else if (systems.ContainsKey(SystemTypes.Laboratory)) sys = systems[SystemTypes.Laboratory];
            else if (systems.ContainsKey(SystemTypes.HeliSabotage)) sys = systems[SystemTypes.HeliSabotage];

            ICriticalSabotage critical;
            if (sys != null && // サボタージュ存在確認
                (critical = sys.TryCast<ICriticalSabotage>()) != null && // キャスト可能確認
                critical.Countdown < 0f) // タイムアップ確認
            {
                // リアクターサボタージュ
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Impostor);
                reason = GameOverReason.ImpostorsBySabotage;
                critical.ClearSabotage();
                return true;
            }

            return false;
        }
    }
}