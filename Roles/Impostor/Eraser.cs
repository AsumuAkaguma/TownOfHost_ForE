using UnityEngine;

using System.Collections.Generic;
using System.Linq;

using AmongUs.GameOptions;

using TownOfHostForE.Roles.Core;
using TownOfHostForE.Roles.Core.Interfaces;
using static TownOfHostForE.Translator;
using Hazel;
using TownOfHostForE.Roles.Core.Class;

namespace TownOfHostForE.Roles.Impostor
{
    public sealed class Eraser : ShapeSwitchManager, IImpostor
    {
        /// <summary>
        ///  20000:TOH4E役職
        ///   1000:陣営 1:crew 2:imp 3:Third 4:Animals
        ///    100:役職ID
        /// </summary>
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Eraser),
                player => new Eraser(player),
                CustomRoles.Eraser,
                () => RoleTypes.Shapeshifter,
                CustomRoleTypes.Impostor,
                22700,
                SetUpOptionItem,
                "イレイサー"
            );
        public Eraser(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            AbilityCool = OptionAbilityCool.GetFloat();
            AbilityCount = OptionAbilityCount.GetInt();
            TargetId = byte.MaxValue;

        }
        private static OptionItem OptionAbilityCool;
        private static OptionItem OptionAbilityCount;
        enum OptionName
        {
            EraserAbilityCool,
            EraserAbilityCount,
        }
        private static float AbilityCool;
        private static float AbilityCount;
        public byte TargetId;


        private static void SetUpOptionItem()
        {
            OptionAbilityCool = FloatOptionItem.Create(RoleInfo, 10, OptionName.EraserAbilityCool, new(5f, 900f, 5f), 60f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptionAbilityCount = IntegerOptionItem.Create(RoleInfo, 11, OptionName.EraserAbilityCount, new(1, 15, 1), 2, false)
                .SetValueFormat(OptionFormat.Players);
        }
        public override void ApplyGameOptions(IGameOptions opt)
        {
            AURoleOptions.ShapeshifterCooldown = AbilityCool;
            //AURoleOptions.ShapeshifterDuration = 1f;
        }


        public override string GetAbilityButtonText() => GetString("EraserWait");
        public override string GetProgressText(bool comms = false) => Utils.ColorString(AbilityCount > 0 ? Color.red : Color.gray, $"({AbilityCount})");
        public override void AfterMeetingTasks()
        {
            if (Player.IsAlive())
                Player.RpcResetAbilityCooldown();

            if (TargetId != byte.MaxValue)
            {
                var target = Utils.GetPlayerById(TargetId);
                if (target == null) return;
                target.RpcSetCustomRole(CustomRoles.Crewmate);
                TargetId = byte.MaxValue;
                Logger.Info($"Make Crew:{target.name}", "Eraser");
            }

            //称号とかつけるよう
            new LateTask(() =>
            {
                Utils.NotifyRoles(NoCache: true);
            }, 0.5f);

        }
        private void SendRPC()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            using var sender = CreateSender(CustomRPC.EraserSync);
            sender.Writer.Write(TargetId);
            sender.Writer.Write(AbilityCount);
        }
        public override void ReceiveRPC(MessageReader reader, CustomRPC rpcType)
        {
            if (rpcType != CustomRPC.EraserSync) return;
            TargetId = reader.ReadByte();
            AbilityCount = reader.ReadInt32();
        }

        public override void ShapeSwitch()
        {

            Dictionary<float, byte> KillDic = new();
            var KillRange = NormalGameOptionsV09.KillDistances[Mathf.Clamp(Main.NormalOptions.KillDistance, 0, 2)];

            //範囲に入っている人算出
            foreach (var pc in Main.AllAlivePlayerControls)
            {
                if (!pc.IsAlive()) continue;

                if (pc == Player) continue;

                if (pc.GetCustomRole().GetCustomRoleTypes() == CustomRoleTypes.Impostor) continue;

                float tempTargetDistance = Vector2.Distance(Player.transform.position, pc.transform.position); ;

                bool checker = tempTargetDistance <= KillRange && pc.CanMove;

                if (!checker) continue;

                KillDic.Add(tempTargetDistance, pc.PlayerId);
            }

            if (KillDic.Count == 0) return;

            //距離が一番近い人算出
            var killTargetKeys = KillDic.Keys.OrderBy(x => x).FirstOrDefault();
            TargetId = KillDic[killTargetKeys];
            AbilityCount--;
            Utils.NotifyRoles();
        }
    }
}