using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using AmongUs.GameOptions;

using TownOfHostForE.Roles.Core;
using TownOfHostForE.Roles.Core.Interfaces;
using static TownOfHostForE.Translator;
using Hazel;
using MS.Internal.Xml.XPath;
using Epic.OnlineServices.Presence;

namespace TownOfHostForE.Roles.Impostor;

public sealed class RemoteCharger : RoleBase, IImpostor
{
    /// <summary>
    ///  20000:TOH4E役職
    ///   1000:陣営 1:crew 2:imp 3:Third 4:Animals
    ///    100:役職ID
    /// </summary>
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(RemoteCharger),
            player => new RemoteCharger(player),
            CustomRoles.RemoteCharger,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            22920,
            SetupOptionItem,
            "リモートチャージャー"
        );
    public RemoteCharger(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        CurrentKillCooldown = KillCooldown.GetFloat();
        oneChargeMinusCool = OptionOneChargeMinusCool.GetFloat();
    }

    private enum OptionName
    {
        ChargerMinusCool,
    }

    //チャージ数
    private int chargeCount = 0;
    //最大チャージ数
    private readonly int maxChargeCount = 3;
    //1チャージごとの減少量
    private float oneChargeMinusCool;
    //このターンにキルしているか
    private bool thisTurnKill = false;

    //1チャージごとの減少量
    private static OptionItem OptionOneChargeMinusCool;
    //キルクール
    static OptionItem KillCooldown;
    //キルクール
    public float CurrentKillCooldown = 30;

    public float CalculateKillCooldown()
    {
        float tempKillCool = CurrentKillCooldown - (oneChargeMinusCool * chargeCount);
        if (tempKillCool <= 0) tempKillCool = 0.1f;
        return tempKillCool;
    }

    private static void SetupOptionItem()
    {
        KillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 2.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionOneChargeMinusCool = FloatOptionItem.Create(RoleInfo, 11, OptionName.ChargerMinusCool, new(0f, 30f, 2.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override string GetProgressText(bool comms = false) => Utils.ColorString(Color.red, $"『{ChargeText()}』");

    private string ChargeText()
    {
        string returntext = "";
        switch (chargeCount)
        {
            case 1:
                returntext = "■";
                break;
            case 2:
                returntext = "■■";
                break;
            case 3:
                returntext = "■■■";
                break;
            default:
                returntext = "×";
                break;

        }
        return returntext;
    }

    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        //停電もしくはコミュサボ中はキルできない。
        if (Utils.IsActive(SystemTypes.Electrical) || Utils.IsActive(SystemTypes.Comms))
        {
            info.DoKill = false;
        }
    }
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        //実際にキルを行う
        if (!info.DoKill) return;

        thisTurnKill = true;
        if (chargeCount > 0)
        {
            chargeCount--;
            Utils.NotifyRoles(SpecifySeer: Player);
        }

        Player.ResetKillCooldown();
        Player.SyncSettings();//キルクール処理を同期
    }
    public override void OnStartMeeting()
    {
        if (thisTurnKill) return;

        if (chargeCount < maxChargeCount)
        {
            chargeCount++;
            Utils.NotifyRoles(SpecifySeer: Player);
        }
    }

    public override void AfterMeetingTasks()
    {
        thisTurnKill = false;
    }
}
