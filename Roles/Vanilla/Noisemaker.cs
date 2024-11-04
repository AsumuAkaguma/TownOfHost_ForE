using AmongUs.GameOptions;
using TownOfHostForE.Roles.Core;

namespace TownOfHostForE.Roles.Vanilla;

public sealed class Noisemaker : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Noisemaker),
            player => new Noisemaker(player),
            RoleTypes.Noisemaker,
            "#8cffff"
        );
    public Noisemaker(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }
}

