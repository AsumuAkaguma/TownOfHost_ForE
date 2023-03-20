using System;
using System.Collections.Generic;
using UnityEngine;
using Hazel;
using AmongUs.GameOptions;

using TownOfHost.Roles.AddOns.Crewmate;
using static TownOfHost.Translator;

namespace TownOfHost.Roles.Core;

public abstract class RoleBase : IDisposable
{
    public PlayerControl Player;
    /// <summary>
    /// タスクは持っているか。
    /// 初期値はクルー役職のみ持つ
    /// </summary>
    public bool HasTasks;
    /// <summary>
    /// キル能力を持っているか
    /// </summary>
    public bool CanKill;
    /// <summary>
    /// キル動作 == キルの役職か
    /// </summary>
    public bool IsKiller;
    public RoleBase(
        SimpleRoleInfo roleInfo,
        PlayerControl player,
        bool? hasTasks = null,
        bool? canKill = null
    )
    {
        Player = player;
        HasTasks = hasTasks ?? roleInfo.CustomRoleType == CustomRoleTypes.Crewmate;
        CanKill = canKill ?? roleInfo.BaseRoleType is RoleTypes.Impostor or RoleTypes.Shapeshifter;
        IsKiller = CanKill;

        CustomRoleManager.AllActiveRoles.Add(this);
    }
    public void Dispose()
    {
        Player = null;
        OnDestroy();
        CustomRoleManager.AllActiveRoles.Remove(this);
    }
    public bool Is(PlayerControl player)
    {
        return player.PlayerId == Player.PlayerId;
    }
    /// <summary>
    /// インスタンス作成後すぐに呼ばれる関数
    /// </summary>
    public virtual void Add()
    { }
    /// <summary>
    /// ロールベースが破棄されるときに呼ばれる関数
    /// </summary>
    public virtual void OnDestroy()
    { }
    /// <summary>
    /// RoleBase専用のRPC送信クラス
    /// 自身のPlayerIdを自動的に送信する
    /// </summary>
    protected class RoleRPCSender : IDisposable
    {
        public MessageWriter Writer;
        public RoleRPCSender(RoleBase role, CustomRPC rpcType)
        {
            Writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)rpcType, SendOption.Reliable, -1);
            Writer.Write(role.Player.PlayerId);
        }
        public void Dispose()
        {
            AmongUsClient.Instance.FinishRpcImmediately(Writer);
        }
    }
    /// <summary>
    /// RPC送信クラスの作成
    /// PlayerIdは自動的に追記されるので意識しなくてもよい。
    /// </summary>
    /// <param name="rpcType">送信するCustomRPC</param>
    /// <returns>送信に使用するRoleRPCSender</returns>
    protected RoleRPCSender CreateSender(CustomRPC rpcType)
    {
        return new RoleRPCSender(this, rpcType);
    }
    /// <summary>
    /// RPCを受け取った時に呼ばれる関数
    /// RoleRPCSenderで送信されたPlayerIdは削除されて渡されるため意識しなくてもよい。
    /// </summary>
    /// <param name="reader">届いたRPCの情報</param>
    /// <param name="rpcType">届いたCustomRPC</param>
    public virtual void ReceiveRPC(MessageReader reader, CustomRPC rpcType)
    { }
    /// <summary>
    /// キルボタンを使えるかどうか
    /// </summary>
    /// <returns>trueを返した場合、キルボタンを使える</returns>
    public virtual bool CanUseKillButton() => CanKill;
    /// <summary>
    /// キルクールダウンを設定する関数
    /// </summary>
    public virtual float SetKillCooldown() => 30f;
    /// <summary>
    /// BuildGameOptionsで呼ばれる関数
    /// </summary>
    public virtual void ApplyGameOptions(IGameOptions opt)
    { }

    // == CheckMurder関連処理 ==
    /// <summary>
    /// キラーとしてのCheckMurder処理
    /// 通常キルはブロックされることを考慮しなくてもよい。
    /// 通常キル以外の能力はinfo.CanKill=falseの場合は効果発揮しないよう実装する。
    /// キルを行わない場合はinfo.DoKill=falseとする。
    /// </summary>
    /// <param name="info">キル関係者情報</param>
    public virtual void OnCheckMurderAsKiller(MurderInfo info) { }

    /// <summary>
    /// ターゲットとしてのCheckMurder処理
    /// キラーより先に判定
    /// キル出来ない状態(無敵など)はinfo.CanKill=falseとしてtrueを返す
    /// キル行為自体をなかったことにする場合はfalseを返す。
    /// </summary>
    /// <param name="info">キル関係者情報</param>
    /// <returns>false:キル行為を起こさせない</returns>
    public virtual bool OnCheckMurderAsTarget(MurderInfo info) => true;

    // ==MurderPlayer関連処理 ==
    /// <summary>
    /// キラーとしてのMurderPlayer処理
    /// </summary>
    /// <param name="info">キル関係者情報</param>
    public virtual void OnMurderPlayerAsKiller(MurderInfo info)
    { }

    /// <summary>
    /// ターゲットとしてのMurderPlayer処理
    /// </summary>
    /// <param name="info">キル関係者情報</param>
    public virtual void OnMurderPlayerAsTarget(MurderInfo info)
    { }

    /// <summary>
    /// シェイプシフト時に呼ばれる関数
    /// 自分自身について呼ばれるため本人確認不要
    /// Host以外も呼ばれるので注意
    /// </summary>
    /// <param name="target">変身先</param>
    public virtual void OnShapeshift(PlayerControl target)
    { }

    /// <summary>
    /// タスクターンに常時呼ばれる関数
    /// 自分自身について呼ばれるため本人確認不要
    /// Host以外も呼ばれるので注意
    /// </summary>
    public virtual void OnFixedUpdate()
    { }

    /// <summary>
    /// 通報時に呼ばれる関数
    /// 通報に関係ないプレイヤーも呼ばれる
    /// </summary>
    /// <param name="reporter">通報したプレイヤー</param>
    /// <param name="target">通報されたプレイヤー</param>
    /// <returns>falseを返すと通報がキャンセルされます</returns>
    public virtual bool OnReportDeadBody(PlayerControl reporter, GameData.PlayerInfo target) => true;

    /// <summary>
    /// ミーティングが始まった時に呼ばれる関数
    /// </summary>
    public virtual void OnStartMeeting()
    { }

    /// <summary>
    /// タスクターンが始まる直前に毎回呼ばれる関数
    /// </summary>
    public virtual void AfterMeetingTasks()
    { }

    /// <summary>
    /// タスクが一個完了するごとに呼ばれる関数
    /// </summary>
    public virtual void OnCompleteTask()
    { }

    // NameSystem
    // 名前は下記の構成で表示される
    // [Role][Progress]
    // [Name][Mark]
    // [Lower][suffix]
    // Progress:タスク進捗/残弾等の状態表示
    // Mark:役職能力によるターゲットマークなど
    // Lower:役職用追加文字情報。Modの場合画面下に表示される。
    // Suffix:ターゲット矢印などの追加情報。

    /// <summary>
    /// 役職名の横に出るテキスト
    /// </summary>
    /// <param name="comms">コミュサボ中扱いするかどうか</param>
    public virtual string GetProgressText(bool comms = false)
    {
        var playerId = Player.PlayerId;
        //タスクテキスト
        var taskState = Main.PlayerStates?[playerId].GetTaskState();
        if (!taskState.hasTasks) return "";

        Color TextColor = Color.yellow;
        var info = Utils.GetPlayerInfoById(playerId);
        var TaskCompleteColor = Utils.HasTasks(info) ? Color.green : Utils.GetRoleColor(info.GetCustomRole()).ShadeColor(0.5f); //タスク完了後の色
        var NonCompleteColor = Utils.HasTasks(info) ? Color.yellow : Color.white; //カウントされない人外は白色

        if (Workhorse.IsThisRole(playerId))
            NonCompleteColor = Workhorse.RoleColor;

        var NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;

        TextColor = comms ? Color.gray : NormalColor;
        string Completed = comms ? "?" : $"{taskState.CompletedTasksCount}";
        return Utils.ColorString(TextColor, $"({Completed}/{taskState.AllTasksCount})");
    }
    /// <summary>
    /// seerが自分であるときのMark
    /// seer,seenともに自分以外であるときに表示したい場合は同じ引数でstaticとして実装し
    /// CustomRoleManager.MarkOthersに登録する
    /// </summary>
    /// <param name="seer">見る側</param>
    /// <param name="seen">見られる側</param>
    /// <param name="isForMeeting">会議中フラグ</param>
    /// <returns>構築したMark</returns>
    public virtual string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false) => "";
    /// <summary>
    /// seerが自分であるときのLowerTex
    /// seer,seenともに自分以外であるときに表示したい場合は同じ引数でstaticとして実装し
    /// CustomRoleManager.LowerOthersに登録する
    /// </summary>
    /// <param name="seer">見る側</param>
    /// <param name="seen">見られる側</param>
    /// <param name="isForMeeting">会議中フラグ</param>
    /// <param name="isForHud">ModでHudとして表示する場合</param>
    /// <returns>構築したLowerText</returns>
    public virtual string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false) => "";
    /// <summary>
    /// seer自分であるときのSuffix
    /// seer,seenともに自分以外であるときに表示したい場合は同じ引数でstaticとして実装し
    /// CustomRoleManager.SuffixOthersに登録する
    /// </summary>
    /// <param name="seer">見る側</param>
    /// <param name="seen">見られる側</param>
    /// <param name="isForMeeting">会議中フラグ</param>
    /// <returns>構築したMark</returns>
    public virtual string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false) => "";

    /// <summary>
    /// シェイプシフトボタンを変更します
    /// </summary>
    public virtual string GetKillButtonText() => GetString(StringNames.KillLabel);
    /// <summary>
    /// シェイプシフトボタンのテキストを変更します
    /// </summary>
    public virtual string GetAbilityButtonText()
    {
        StringNames str = Player.Data.Role.Role switch
        {
            RoleTypes.Engineer => StringNames.VentAbility,
            RoleTypes.Scientist => StringNames.VitalsAbility,
            RoleTypes.Shapeshifter => StringNames.ShapeshiftAbility,
            RoleTypes.GuardianAngel => StringNames.ProtectAbility,
            RoleTypes.ImpostorGhost or RoleTypes.CrewmateGhost => StringNames.HauntAbilityName,
            _ => StringNames.ErrorInvalidName
        };
        return GetString(str);
    }
}