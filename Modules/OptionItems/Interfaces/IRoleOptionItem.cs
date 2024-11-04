using TownOfHostForE.Roles.Core;
using UnityEngine;

namespace TownOfHostForE.Modules.OptionItems.Interfaces;

public interface IRoleOptionItem
{
    public CustomRoles RoleId { get; }
    public Color RoleColor { get; }
}
