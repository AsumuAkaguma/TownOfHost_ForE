using UnityEngine;
using AmongUs.GameOptions;

using TownOfHostForE.Roles.Neutral;
using TownOfHostForE.Roles.Core;
using TownOfHostForE.Roles.Core.Interfaces;
using static TownOfHostForE.Translator;

namespace TownOfHostForE.Roles.Impostor
{
    public sealed class SerialKiller : RoleBase, IImpostor
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(SerialKiller),
                player => new SerialKiller(player),
                CustomRoles.SerialKiller,
                () => RoleTypes.Shapeshifter,
                CustomRoleTypes.Impostor,
                10200,
                SetUpOptionItem,
                "シリアルキラー"
            );
        public SerialKiller(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            KillCooldown = OptionKillCooldown.GetFloat();
            TimeLimit = OptionTimeLimit.GetFloat();

            SuicideTimer = null;
        }
        private static OptionItem OptionKillCooldown;
        private static OptionItem OptionTimeLimit;
        enum OptionName
        {
            SerialKillerLimit
        }
        private static float KillCooldown;
        private static float TimeLimit;

        public bool CanBeLastImpostor { get; } = false;
        public float? SuicideTimer;

        private static void SetUpOptionItem()
        {
            OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(2.5f, 180f, 2.5f), 20f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptionTimeLimit = FloatOptionItem.Create(RoleInfo, 11, OptionName.SerialKillerLimit, new(5f, 900f, 5f), 60f, false)
                .SetValueFormat(OptionFormat.Seconds);
        }
        public float CalculateKillCooldown() => KillCooldown;
        public override void ApplyGameOptions(IGameOptions opt)
        {
            AURoleOptions.ShapeshifterCooldown = HasKilled() ? TimeLimit : 255f;
            AURoleOptions.ShapeshifterDuration = 1f;
        }
        ///<summary>
        ///シリアルキラー＋生存＋一人以上キルしている
        ///</summary>
        public bool HasKilled()
            => Player != null && Player.IsAlive() && MyState.GetKillCount(true) > 0;
        public void OnCheckMurderAsKiller(MurderInfo info)
        {
            var killer = info.AttemptKiller;
            SuicideTimer = null;
            killer.MarkDirtySettings();
        }
        public override bool OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
        {
            SuicideTimer = null;
            return true;
        }
        public override void OnFixedUpdate(PlayerControl player)
        {
            if (AmongUsClient.Instance.AmHost && !ExileController.Instance)
            {
                if (!HasKilled())
                {
                    SuicideTimer = null;
                    return;
                }
                if (SuicideTimer == null) //タイマーがない
                {
                    SuicideTimer = 0f;
                    Player.RpcResetAbilityCooldown();
                }
                else if (SuicideTimer >= TimeLimit)
                {
                    //自爆時間が来たとき
                    MyState.DeathReason = CustomDeathReason.Suicide;//死因：自殺
                    Player.RpcMurderPlayer(Player);//自殺させる
                    //ExileControllerWrapUpPatch.REIKAITENSOU(Player.PlayerId, CustomDeathReason.Suicide);
                    //foreach (var target in Main.AllAlivePlayerControls)
                    //{
                    //    if (target != Player && target.Is(CustomRoleTypes.Impostor))
                    //    {
                    //        Utils.KillFlash(target);
                    //    }
                    //}
                    SuicideTimer = null;
                }
                else
                    SuicideTimer += Time.fixedDeltaTime;//時間をカウント
            }
        }
        public override bool CanUseAbilityButton() => HasKilled();
        public override string GetAbilityButtonText() => GetString("SerialKillerSuicideButtonText");
        public override void OnSpawn(bool initialState)
        {
            if (Player.IsAlive())
            {
                if (HasKilled())
                    SuicideTimer = 0f;
            }
        }
        public void OnSchrodingerCatKill(SchrodingerCat schrodingerCat)
        {
            SuicideTimer = null;
        }
    }
}