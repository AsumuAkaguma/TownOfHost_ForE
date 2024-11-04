using AmongUs.GameOptions;
using TownOfHostForE.Roles.Core;

namespace TownOfHostForE.Roles.Vanilla;

public sealed class Tracker : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Tracker),
            player => new Tracker(player),
            RoleTypes.Tracker,
            "#8cffff"
        );
    public Tracker(PlayerControl player)
        : base(
            RoleInfo,
            player
    )
    { }
   
}
