using System;
using System.Linq;
using System.Threading.Tasks;
using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;

using TownOfHostForE.Roles.Core;
using static TownOfHostForE.Translator;
using TownOfHostForE.Patches;

namespace TownOfHostForE
{
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
    class SetUpRoleTextCoBeginPatch
    {
        public static void Postfix(IntroCutscene __instance, ref Il2CppSystem.Collections.IEnumerator __result)
        {
            //ShowRoleに直接パッチあて出来ないためCoBegin中にパッチを当てる
            var patcher = new CoroutinPatcher(__result);
            //ShowRoleはステートマシンクラスになっているためその実行前にパッチを当てる
            //元々Postfixだが、タイミング的にはPrefixの方が適切なのでPrefixに当てる
            patcher.AddPrefix(typeof(IntroCutscene._ShowRole_d__41), () => SetUpRoleTextPatch.Postfix(__instance));
            __result = patcher.EnumerateWithPatch();
        }
    }
    // Patchが当たらないが念のためコメントアウト
    //[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.ShowRole))]
    class SetUpRoleTextPatch
    {
        public static void Postfix(IntroCutscene __instance)
        {
            if (!GameStates.IsModHost) return;
            _ = new LateTask(() =>
            {
                CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();
                if (!role.IsVanilla())
                {
                    __instance.YouAreText.color = Utils.GetRoleColor(role);
                    __instance.RoleText.text = Utils.GetRoleName(role);
                    __instance.RoleText.color = Utils.GetRoleColor(role);
                    __instance.RoleBlurbText.color = Utils.GetRoleColor(role);

                    __instance.RoleBlurbText.text = PlayerControl.LocalPlayer.GetRoleInfo();
                }

                foreach (var subRole in PlayerState.GetByPlayerId(PlayerControl.LocalPlayer.PlayerId).SubRoles)
                    __instance.RoleBlurbText.text += "\n" + Utils.ColorString(Utils.GetRoleColor(subRole), GetString($"{subRole}Info"));
                __instance.RoleText.text += Utils.GetSubRolesText(PlayerControl.LocalPlayer.PlayerId);

            }, 0.01f, "Override Role Text");

        }
    }
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
    class CoBeginPatch
    {
        public static void Prefix()
        {
            var logger = Logger.Handler("Info");
            logger.Info("------------名前表示------------");
            foreach (var pc in Main.AllPlayerControls)
            {
                logger.Info($"{(pc.AmOwner ? "[*]" : ""),-3}{pc.PlayerId,-2}:{pc.name.PadRightV2(20)}:{pc.cosmetics.nameText.text}({Palette.ColorNames[pc.Data.DefaultOutfit.ColorId].ToString().Replace("Color", "")})");
                pc.cosmetics.nameText.text = pc.name;
            }
            logger.Info("----------役職割り当て----------");
            foreach (var pc in Main.AllPlayerControls)
            {
                logger.Info($"{(pc.AmOwner ? "[*]" : ""),-3}{pc.PlayerId,-2}:{pc?.Data?.PlayerName?.PadRightV2(20)}:{pc.GetAllRoleName().RemoveHtmlTags()}");
            }
            logger.Info("--------------環境--------------");
            foreach (var pc in Main.AllPlayerControls)
            {
                try
                {
                    var text = pc.AmOwner ? "[*]" : "   ";
                    text += $"{pc.PlayerId,-2}:{pc.Data?.PlayerName?.PadRightV2(20)}:{pc.GetClient()?.PlatformData?.Platform.ToString()?.Replace("Standalone", ""),-11}";
                    if (Main.playerVersion.TryGetValue(pc.PlayerId, out PlayerVersion pv))
                        text += $":Mod({pv.forkId}/{pv.version}:{pv.tag})";
                    else text += ":Vanilla";
                    logger.Info(text);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "Platform");
                }
            }
            logger.Info("------------基本設定------------");
            var tmp = GameOptionsManager.Instance.CurrentGameOptions.ToHudString(GameData.Instance ? GameData.Instance.PlayerCount : 10).Split("\r\n").Skip(1);
            foreach (var t in tmp) logger.Info(t);
            logger.Info("------------詳細設定------------");
            foreach (var o in OptionItem.AllOptions)
                if (!o.IsHiddenOn(Options.CurrentGameMode) && (o.Parent == null ? !o.GetString().Equals("0%") : o.Parent.GetBool()))
                    logger.Info($"{(o.Parent == null ? o.Name.PadRightV2(40) : $"┗ {o.Name}".PadRightV2(41))}:{o.GetString().RemoveHtmlTags()}");
            logger.Info("-------------その他-------------");
            logger.Info($"プレイヤー数: {Main.AllPlayerControls.Count()}人");
            Main.AllPlayerControls.Do(x => PlayerState.GetByPlayerId(x.PlayerId).InitTask(x));
            GameData.Instance.RecomputeTaskCounts();
            TaskState.InitialTotalTasks = GameData.Instance.TotalTasks;

            //ペット付与
            PetSettings.SetPetRoleInPet();

            Utils.NotifyRoles();

            GameStates.InGame = true;
        }
    }
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate))]
    class BeginCrewmatePatch
    {
        public static void Prefix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> teamToDisplay)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoleTypes.Neutral) || PlayerControl.LocalPlayer.Is(CustomRoles.StrayWolf) || PlayerControl.LocalPlayer.Is(CustomRoleTypes.Animals))
            {
                //ぼっち役職
                var soloTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
                soloTeam.Add(PlayerControl.LocalPlayer);
                teamToDisplay = soloTeam;
            }
        }
        public static void Postfix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> teamToDisplay)
        {
            //チーム表示変更
            CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();

            if (role.GetRoleInfo()?.IntroSound is AudioClip introSound)
            {
                PlayerControl.LocalPlayer.Data.Role.IntroSound = introSound;
            }
            if (!Options.ChangeIntro.GetBool())
            {
                switch (role.GetCustomRoleTypes())
                {
                    case CustomRoleTypes.Neutral:
                        __instance.TeamTitle.text = GetString("Neutral");
                        __instance.TeamTitle.color = Color.gray;
                        //__instance.TeamTitle.text = Utils.GetRoleName(role);
                        //__instance.TeamTitle.color = Utils.GetRoleColor(role);
                        __instance.ImpostorText.gameObject.SetActive(true);
                        __instance.ImpostorText.text = GetString("NeutralInfo");
                        __instance.BackgroundBar.material.color = Color.gray;
                        //if (!Options.CurrentGameMode.IsOneNightMode())
                            StartFadeIntro(__instance, Color.gray, Utils.GetRoleColor(role));
                        break;
                    case CustomRoleTypes.Animals:
                        __instance.TeamTitle.text = Utils.GetRoleName(role);
                        __instance.TeamTitle.color = Utils.GetRoleColor(role);
                        __instance.ImpostorText.gameObject.SetActive(true);
                        __instance.ImpostorText.text = GetString("TeamAnimals");
                        __instance.BackgroundBar.material.color = Utils.GetRoleColor(role);
                        break;
                    case CustomRoleTypes.Madmate:
                        //if (!Options.CurrentGameMode.IsOneNightMode())
                            StartFadeIntro(__instance, Palette.CrewmateBlue, Palette.ImpostorRed);
                        PlayerControl.LocalPlayer.Data.Role.IntroSound = RoleManager.Instance.AllRoles.Where((role) => role.Role == RoleTypes.Impostor).FirstOrDefault().IntroSound;
                        break;
                }
                switch (role)
                {
                    case CustomRoles.Jackal:
                    case CustomRoles.JClient:
                        __instance.TeamTitle.text = Utils.GetRoleName(CustomRoles.Jackal);
                        __instance.TeamTitle.color = Utils.GetRoleColor(CustomRoles.Jackal);
                        __instance.ImpostorText.gameObject.SetActive(true);
                        __instance.ImpostorText.text = GetString("TeamJackal");
                        __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Jackal);
                        break;
                    
                    case CustomRoles.MadSheriff:
                        __instance.ImpostorText.gameObject.SetActive(true);
                        var numImpostors = Main.NormalOptions.NumImpostors;
                        __instance.ImpostorText.text = numImpostors == 1
                            ? DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.NumImpostorsS)
                            : __instance.ImpostorText.text = string.Format(DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.NumImpostorsP), numImpostors);
                        __instance.ImpostorText.text = __instance.ImpostorText.text.Replace("[FF1919FF]", "<color=#FF1919FF>").Replace("[]", "</color>");
                        break;
                    
                }
            }
            else
            {
                switch (role.GetCustomRoleTypes())
                {
                    case CustomRoleTypes.Neutral:
                        __instance.TeamTitle.text = Utils.GetRoleName(role);
                        __instance.TeamTitle.color = Utils.GetRoleColor(role);
                        __instance.ImpostorText.gameObject.SetActive(true);
                        __instance.ImpostorText.text = role switch
                        {
                            CustomRoles.Egoist => GetString("TeamEgoist"),
                            CustomRoles.Jackal => GetString("TeamJackal"),
                            CustomRoles.JClient => GetString("TeamJackal"),
                            _ => GetString("NeutralInfo"),
                        };
                        __instance.BackgroundBar.material.color = Utils.GetRoleColor(role);
                        break;
                    case CustomRoleTypes.Animals:
                        __instance.TeamTitle.text = Utils.GetRoleName(role);
                        __instance.TeamTitle.color = Utils.GetRoleColor(role);
                        __instance.ImpostorText.gameObject.SetActive(true);
                        __instance.ImpostorText.text = GetString("TeamAnimals");
                        __instance.BackgroundBar.material.color = Utils.GetRoleColor(role);
                        break;
                    case CustomRoleTypes.Madmate:
                        __instance.TeamTitle.text = GetString("Madmate");
                        __instance.TeamTitle.color = Utils.GetRoleColor(CustomRoles.Madmate);
                        __instance.ImpostorText.text = GetString("TeamImpostor");
                        StartFadeIntro(__instance, Palette.CrewmateBlue, Palette.ImpostorRed);
                        break;
                }
            }
            switch (role)
            {
                case CustomRoles.StrayWolf:
                    __instance.TeamTitle.text = GetString("Impostor");
                    __instance.TeamTitle.color = Palette.ImpostorRed;
                    __instance.ImpostorText.gameObject.SetActive(false);
                    __instance.BackgroundBar.material.color = Palette.ImpostorRed;
                    break;
                case CustomRoles.Sheriff:
                case CustomRoles.Hunter:
                case CustomRoles.SillySheriff:
                case CustomRoles.Metaton:
                    __instance.BackgroundBar.material.color = Palette.CrewmateBlue;
                    __instance.ImpostorText.gameObject.SetActive(true);
                    var numImpostors = Main.NormalOptions.NumImpostors;
                    var text = numImpostors == 1
                        ? GetString(StringNames.NumImpostorsS)
                        : string.Format(GetString(StringNames.NumImpostorsP), numImpostors);
                    __instance.ImpostorText.text = text.Replace("[FF1919FF]", "<color=#FF1919FF>").Replace("[]", "</color>");
                    break;

                case CustomRoles.GM:
                    __instance.TeamTitle.text = Utils.GetRoleName(role);
                    __instance.TeamTitle.color = Utils.GetRoleColor(role);
                    __instance.BackgroundBar.material.color = Utils.GetRoleColor(role);
                    __instance.ImpostorText.gameObject.SetActive(false);
                    break;
            }

            if (Input.GetKey(KeyCode.RightShift))
            {
                __instance.TeamTitle.text = Main.ModName;
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = "https://github.com/tukasa0001/TownOfHost" +
                    "\r\nOut Now on Github";
                __instance.TeamTitle.color = Color.cyan;
                StartFadeIntro(__instance, Color.cyan, Color.yellow);
            }
            if (Input.GetKey(KeyCode.RightControl))
            {
                __instance.TeamTitle.text = "Discord Server";
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = "https://discord.gg/v8SFfdebpz";
                __instance.TeamTitle.color = Color.magenta;
                StartFadeIntro(__instance, Color.magenta, Color.magenta);
            }
        }
        private static async void StartFadeIntro(IntroCutscene __instance, Color start, Color end)
        {
            await Task.Delay(2000);
            int milliseconds = 0;
            while (true)
            {
                await Task.Delay(20);
                milliseconds += 20;
                float time = (float)milliseconds / (float)500;
                Color LerpingColor = Color.Lerp(start, end, time);
                if (__instance == null || milliseconds > 500)
                {
                    Logger.Info("ループを終了します", "StartFadeIntro");
                    break;
                }
                __instance.BackgroundBar.material.color = LerpingColor;
            }
        }
    }
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor))]
    class BeginImpostorPatch
    {
        public static bool Prefix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> yourTeam)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Sheriff)
                ||PlayerControl.LocalPlayer.Is(CustomRoles.Hunter)
                ||PlayerControl.LocalPlayer.Is(CustomRoles.Metaton)
                ||PlayerControl.LocalPlayer.Is(CustomRoles.SillySheriff))
            {
                //シェリフの場合はキャンセルしてBeginCrewmateに繋ぐ
                yourTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
                yourTeam.Add(PlayerControl.LocalPlayer);
                foreach (var pc in Main.AllPlayerControls)
                {
                    if (!pc.AmOwner) yourTeam.Add(pc);
                }
                __instance.BeginCrewmate(yourTeam);
                __instance.overlayHandle.color = Palette.CrewmateBlue;
                return false;
            }
            BeginCrewmatePatch.Prefix(__instance, ref yourTeam);
            return true;
        }
        public static void Postfix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> yourTeam)
        {
            BeginCrewmatePatch.Postfix(__instance, ref yourTeam);
        }
    }
    [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
    class IntroCutsceneDestroyPatch
    {
        public static void Postfix(IntroCutscene __instance)
        {
            if (!GameStates.IsInGame) return;
            Main.isFirstTurn = true;

            var mapId = Main.NormalOptions.MapId;
            // エアシップではまだ湧かない
            if ((MapNames)mapId != MapNames.Airship)
            {
                foreach (var state in PlayerState.AllPlayerStates.Values)
                {
                    state.HasSpawned = true;
                }
            }

            if (AmongUsClient.Instance.AmHost)
            {
                if (mapId != 4)
                {
                    Main.AllPlayerControls.Do(pc =>
                    {
                        pc.GetRoleClass()?.OnSpawn(true);
                        pc.SyncSettings();
                        pc.RpcResetAbilityCooldown();
                    });
                    if (Options.FixFirstKillCooldown.GetBool())
                        _ = new LateTask(() =>
                        {
                            Main.AllPlayerControls.Do(pc => pc.SetKillCooldown(Main.AllPlayerKillCooldown[pc.PlayerId] - 2f));
                        }, 2f, "FixKillCooldownTask");
                }
                _ = new LateTask(() => Main.AllPlayerControls.Do(pc => pc.RpcSetRoleDesync(RoleTypes.Shapeshifter, -3)), 2f, "SetImpostorForServer");
                if (PlayerControl.LocalPlayer.Is(CustomRoles.GM))
                {
                    PlayerControl.LocalPlayer.RpcExile();
                    PlayerState.GetByPlayerId(PlayerControl.LocalPlayer.PlayerId).SetDead();
                }
                if (RandomSpawn.IsRandomSpawn())
                {
                    RandomSpawn.SpawnMap map;
                    switch (mapId)
                    {
                        case 0:
                            map = new RandomSpawn.SkeldSpawnMap();
                            Main.AllPlayerControls.Do(map.RandomTeleport);
                            break;
                        case 1:
                            map = new RandomSpawn.MiraHQSpawnMap();
                            Main.AllPlayerControls.Do(map.RandomTeleport);
                            break;
                        case 2:
                            map = new RandomSpawn.PolusSpawnMap();
                            Main.AllPlayerControls.Do(map.RandomTeleport);
                            break;
                        case 5:
                            map = new RandomSpawn.FungleSpawnMap();
                            Main.AllPlayerControls.Do(map.RandomTeleport);
                            break;
                    }
                }

                // そのままだとホストのみDesyncImpostorの暗室内での視界がクルー仕様になってしまう
                var roleInfo = PlayerControl.LocalPlayer.GetCustomRole().GetRoleInfo();
                var amDesyncImpostor = roleInfo?.IsDesyncImpostor == true;
                if (amDesyncImpostor)
                {
                    PlayerControl.LocalPlayer.Data.Role.AffectedByLightAffectors = false;
                }
            }
            Logger.Info("OnDestroy", "IntroCutscene");

            GameStates.InTask = true;
            Logger.Info("タスクフェイズ開始", "Phase");
        }
    }
}