using AmongUs.GameOptions;
using TownOfHostForE.Roles.Core.Interfaces;
using TownOfHostForE.Roles.Core;

namespace TownOfHostForE.Roles.Vanilla;

public sealed class Phantom : RoleBase, IImpostor
{
    public Phantom(PlayerControl player) : base(RoleInfo, player) { }
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.CreateForVanilla(typeof(Phantom), player => new Phantom(player), RoleTypes.Phantom);
}