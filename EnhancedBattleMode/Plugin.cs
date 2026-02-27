using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Internal;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using System.Linq;
using Emote = Lumina.Excel.Sheets.Emote;

namespace EnhancedBattleMode;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IUnlockState UnlockState { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/ebm";

    public unsafe bool Moving => AgentMap.Instance() is not null && AgentMap.Instance()->IsPlayerMoving;

    public Configuration Configuration { get; init; }

    public Plugin()
    {
        ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Use in place of /bm to queue your draw/sheathe actions more intelligently."
        });

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [EnhancedBattleMode] ===A cool log message from Sample Plugin===
        Log.Information($"==={PluginInterface.Manifest.Name} Has Loaded.===");
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
    }


    private static void Assert(bool succeeds, string message)
    {
        if (!succeeds)
            throw new Exception(message);
    }



    public static Emote? FindEmoteByCommand(IDataManager dataManager, string command)
    {
        command = command.ToLowerInvariant();

        foreach (var emote in dataManager.GetExcelSheet<Emote>()!)
        {
            var textCommand = emote.TextCommand.Value;
            if (textCommand.Command.IsEmpty)
            {
                continue;
            }

            // TextCommand.Command is the slash command without the leading '/'
            if ($"/{textCommand.Command.ToString().ToLowerInvariant}" == command)
            {
                return emote;
            }
        }

        return null;
    }
    private static IExposedPlugin? findPlugin(string name)
    {
        IExposedPlugin[] plugins = [.. PluginInterface.InstalledPlugins];
        Log.Information($"Checking {plugins.Length} installed plugins for {name}");
        return plugins.FirstOrDefault(p => p.InternalName == name);
    }

    private void OnCommand(string command, string args)
    {
        if (!ClientState.IsLoggedIn)
        {
            return;
        }
        else
        {
            Assert(Objects.LocalPlayer is not null, "can't find LocalPlayer");
            var cmd = "/echo Enhanced Battle Mode failed to handle your request.";
#pragma warning disable CS8629 // Nullable value type may be null.
            var isSheatheUnlocked = UnlockState.IsEmoteUnlocked(FindEmoteByCommand(DataManager, "/sheathe").Value);
            var isDrawUnlocked = UnlockState.IsEmoteUnlocked(FindEmoteByCommand(DataManager, "/draw").Value);
            var PandorasBox = findPlugin("PandorasBox");
            var ResetEnmityCmd = findPlugin("Reset-dummy-enmity-command");
#pragma warning restore CS8629 // Nullable value type may be null.

            if (Player.Mounted || Player.Mounting || Player.Object!.CurrentMount.HasValue)
            {
                cmd = "/mount clear";
            }
            else if (Condition[ConditionFlag.InCombat] && PluginInterface.InstalledPlugins.Contains(ResetEnmityCmd))
            {
                cmd = "/resetenmityall";
            }
            else if (Condition[ConditionFlag.InCombat] && PluginInterface.InstalledPlugins.Contains(PandorasBox))
            {
                cmd = "/pre";
            }

            if (Condition[ConditionFlag.InCombat])
            {
                cmd = "/battlemode";
            }
            else if (Player.Object!.StatusFlags.HasFlag(StatusFlags.WeaponOut))
            {
                cmd = Moving && isSheatheUnlocked ? "/battlemode off" : "/sheathe";
            }
            else if (!Player.Object!.StatusFlags.HasFlag(StatusFlags.WeaponOut))
            {
                cmd = Moving && isDrawUnlocked ? "/battlemode on" : "/draw";
            }

            if (cmd != null)
            {
                CommandManager.ProcessCommand(cmd);
            }
        }

        /*local function core()
            if Game.Player.InCombat then
                Game.SendChat("/battlemode")
            elseif Game.Player.WeaponDrawn then
                if Game.Player.Moving or not Game.Player.HasEmote("sheathe") then
                    Game.SendChat("/battlemode off")
                else
                    Game.SendChat("/sheathe motion")
                end
                Game.Player.ClearTarget()
            elseif Game.Player.Job.IsBlu or Game.Player.Job.ShortName == "NIN" or Game.Player.Moving or not Game.Player.HasEmote("draw") then
                Game.SendChat("/battlemode on")
            else
                Game.SendChat("/draw motion")
            end
        end

        local function check()
            if Game.Player.Mounted then
                Game.SendChat("/mount clear")
            elseif Game.Player.InCombat and Game.Dalamud.HasPlugin("Reset-dummy-enmity-command") then
                Game.SendChat("/resetenmityall")
                Script.QueueDelay(500)
            elseif Game.Player.InCombat and Game.Dalamud.HasPlugin("PandorasBox") then
                Game.SendChat("/pre")
                Script.QueueDelay(500)
            end
            Script.QueueAction(core)
        end

        Script(check)
        */

    }

}
