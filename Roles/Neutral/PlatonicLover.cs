using System.Collections.Generic;
using AmongUs.GameOptions;

using TownOfHostForE.Modules;
using TownOfHostForE.Roles.Core;
using TownOfHostForE.Roles.Core.Interfaces;

namespace TownOfHostForE.Roles.Neutral;

public sealed class PlatonicLover : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
         SimpleRoleInfo.Create(
            typeof(PlatonicLover),
            player => new PlatonicLover(player),
            CustomRoles.PlatonicLover,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            60400,
            SetupOptionItem,
            "純愛者",
            "#ff6be4",
            true,
            countType: CountTypes.Crew
        );
    public PlatonicLover(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        AddWin = OptionAddWin.GetBool();
    }
    public static OptionItem OptionAddWin;
    enum OptionName
    {
        LoversAddWin,
    }
    public bool isMadeLover;
    public static bool AddWin;

    private static void SetupOptionItem()
    {
        OptionAddWin = BooleanOptionItem.Create(RoleInfo, 10, OptionName.LoversAddWin, false, false);
    }

    public override void Add()
    {
        var playerId = Player.PlayerId;
        isMadeLover = false;
    }
    public float CalculateKillCooldown() => CanUseKillButton() ? 0.1f : 0f;
    public bool CanUseKillButton() => Player.IsAlive() && !isMadeLover;
    public bool CanUseImpostorVentButton() => false;
    public bool CanUseSabotageButton() => false;
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        //キル不可
        info.DoKill = false;
        (var killer, var target) = info.AttemptTuple;

        if (isMadeLover)
        {
            return;
        }

        isMadeLover = true;

        //本処理
        LoversManager.CheckMurderLovers(killer, target);
        //isMadeLover = true;
        //info.DoKill = false;
        //killer.RpcProtectedMurderPlayer(target);
        //target.RpcProtectedMurderPlayer(target);
        //Logger.Info($"{killer.GetNameWithRole()} : 恋人を作った", "PlatonicLover");

        //killer.RpcSetCustomRole(CustomRoles.Lovers);
        //target.RpcSetCustomRole(CustomRoles.Lovers);

        //List<byte> playerIds = new ();
        //playerIds.Add(killer.PlayerId);
        //Main.isLoversLeaders.Add(killer.PlayerId);

        //if (CheckOtherLovers(target.PlayerId,out byte teamLeaderId))
        //{
        //    //リーダー
        //    if (target.PlayerId == teamLeaderId)
        //    {
        //        //相手ラバーズチームを自チームに加える
        //        playerIds.AddRange(Main.LoversPlayersV2[teamLeaderId]);
        //        //相手ラバーズを削除
        //        Main.LoversPlayersV2.Remove(teamLeaderId);
        //        //リーダーとしても削除
        //        Main.isLoversLeaders.Remove(teamLeaderId);
        //        Main.isLoversDeadV2.Remove(teamLeaderId);
        //    }
        //    //巻き添えの人
        //    else
        //    {
        //        Main.LoversPlayersV2[teamLeaderId].Remove(target.PlayerId);
        //        playerIds.Add(target.PlayerId);
        //    }
        //}
        //else
        //{
        //    playerIds.Add(target.PlayerId);
        //}
        //Main.LoversPlayersV2.Add(killer.PlayerId,playerIds);
        //Main.isLoversDeadV2.Add(killer.PlayerId,false);

        //RPC.SyncLoversPlayers();

        //Utils.NotifyRoles();
    }

    public bool OverrideKillButtonText(out string text)
    {
        text = Translator.GetString("PlatonicLoverButtonText");
        return true;
    }
}
