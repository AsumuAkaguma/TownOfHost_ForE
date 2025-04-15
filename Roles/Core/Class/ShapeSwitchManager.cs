using System;
using Sentry.Internal.Extensions;
using TownOfHostForE.Modules;
using TownOfHostForE.GameMode;
using System.Linq;
using static UnityEngine.GraphicsBuffer;
using Il2CppSystem.Collections.Generic;

namespace TownOfHostForE.Roles.Core.Class
{
    public class ShapeSwitchManager : RoleBase
    {
        public ShapeSwitchManager(
        SimpleRoleInfo roleInfo,
        PlayerControl player,
        Func<HasTask> hasTasks = null,
        bool? hasAbility = null
        )
        : base(
            roleInfo,
            player,
            hasTasks,
            hasAbility)
        {
            ShapeSwitchPlayerSkin = null;
        }

        nowCostmetic ShapeSwitchPlayerSkin = null;


        /// <summary>
        /// キノコでシェイプシフト解除で呼ばれる奴。基本そのまま使う
        /// </summary>
        public virtual void MushResetShape()
        {
            ResetSkins();
        }

        /// <summary>
        /// 変身ボタン押下時に発動する能力
        /// </summary>
        public virtual void ShapeSwitch()
        {}

        //------------------//

        public override void Add()
        {
            Main.shapeSwitchPlayerIds.Add(Player.PlayerId);
            ShapeSwitchPlayerSkin = new(Player);

            ResetSkins();
        }

        public override void AfterMeetingTasks()
        {
            Utils.NotifyRoles(NoCache: true);
        }

        public override bool OnCheckShapeshift(PlayerControl target, ref bool animate)
        {
            Player.RpcRejectShapeshift();
            this.ShapeSwitch();
            return false;
        }

        private void ResetSkins()
        {
            PlayerControl pc = PlayerControl.LocalPlayer;
            if (Player.PlayerId == pc.PlayerId)
            {
                var list = Main.AllPlayerControls.Where(x => x.PlayerId != pc.PlayerId);
                pc = list.FirstOrDefault();
            }
            Player.RpcShapeshift(pc, false);

            var sender = CustomRpcSender.Create(name: $"ResetSkin({Player.Data.PlayerName})");

            Player.RpcSetName(Player.Data.PlayerName);

            sender.AutoStartRpc(Player.NetId, (byte)RpcCalls.SetColor)
                .Write(ShapeSwitchPlayerSkin.ColorID)
                .EndRpc();

            sender.AutoStartRpc(Player.NetId, (byte)RpcCalls.SetHatStr)
                .Write(ShapeSwitchPlayerSkin.HatID)
                .EndRpc();

            sender.AutoStartRpc(Player.NetId, (byte)RpcCalls.SetSkinStr)
                .Write(ShapeSwitchPlayerSkin.SkinID)
                .EndRpc();

            sender.AutoStartRpc(Player.NetId, (byte)RpcCalls.SetVisorStr)
                .Write(ShapeSwitchPlayerSkin.VIsorID)
                .EndRpc();

            sender.AutoStartRpc(Player.NetId, (byte)RpcCalls.SetPetStr)
                .Write(ShapeSwitchPlayerSkin.PetID)
                .EndRpc();

            _ = new LateTask(() =>
            {
                Player.SetColor(ShapeSwitchPlayerSkin.ColorID);
                Player.RpcSetHat(ShapeSwitchPlayerSkin.HatID);
                Player.RpcSetSkin(ShapeSwitchPlayerSkin.SkinID);
                Player.RpcSetVisor(ShapeSwitchPlayerSkin.VIsorID);
                Player.SetPet(ShapeSwitchPlayerSkin.PetID);
            }, 0.5f);

            sender.SendMessage();
            //称号とかつけるよう
            new LateTask(() =>
            {
                Utils.NotifyRoles(NoCache: true);
            }, 0.5f);
        }


    }

    public class nowCostmetic
    {
        //コンストラクタ
        public nowCostmetic(PlayerControl pc)
        {
            var outfit = pc.Data.DefaultOutfit;

            _colorId = outfit.ColorId;
            _hatId = outfit.HatId;
            _skinId = outfit.SkinId;
            _visorId = outfit.VisorId;
            _petId = outfit.PetId;

        }

        int _colorId;
        string _hatId;
        string _skinId;
        string _visorId;
        string _petId;

        public int ColorID
        {
            get { return _colorId; }
        }
        public string HatID
        {
            get { return _hatId; }
        }
        public string SkinID
        {
            get { return _skinId; }
        }
        public string VIsorID
        {
            get { return _visorId; }
        }
        public string PetID
        {
            get { return _petId; }
        }
    }
}
