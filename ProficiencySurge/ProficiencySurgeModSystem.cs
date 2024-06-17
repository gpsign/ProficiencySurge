using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace ProficiencySurge;

public class Instance : ModSystem
{
#pragma warning disable CA2211 // Non-constant fields should not be visible
    public static ICoreClientAPI ClientAPI;
#pragma warning restore CA2211 // Non-constant fields should not be visible
    public static ICoreServerAPI ServerAPI;

    public override void Start(ICoreAPI api)
    {
        if (api is ICoreClientAPI clientAPI)
        {
            Debug.Log("Initializing Client API");
            ClientAPI = clientAPI;
            Utils.InitClientApi(ClientAPI);
            ShovelSkill.InitClientApi(ClientAPI);
        }
        if (api is ICoreServerAPI serverAPI)
        {
            Debug.Log("Initializing Server API");
            ServerAPI = serverAPI;

            Utils.InitServerApi(ServerAPI);
            ShovelSkill.InitServerApi(ServerAPI);

            ServerAPI.Event.BreakBlock += BreakBlock;
        }

        if (ServerAPI != null && ClientAPI != null)
            Debug.Log("Instance initialized");
    }

    public static void BreakBlock(
        IServerPlayer byPlayer,
        BlockSelection blockSel,
        ref float dropQuantityMultiplier,
        ref EnumHandling handling
    )
    {
        if (blockSel == null)
            return;

        ShovelSkill.GainXP(byPlayer, blockSel.Block);
    }
}

public class Utils
{
    public static ICoreClientAPI ClientAPI;
#pragma warning restore CA2211 // Non-constant fields should not be visible
    public static ICoreServerAPI ServerAPI;

    public static void InitServerApi(ICoreServerAPI api)
    {
        ServerAPI = api;
    }

    public static void InitClientApi(ICoreClientAPI api)
    {
        ClientAPI = api;
    }

    public static T GetData<T>(string key, T defaultValue = default)
    {
        Debug.Log("Retrieving data from SaveGame...");
        if (ServerAPI.WorldManager.SaveGame == null)
        {
            Debug.Log("Error: ServerAPI is null. Returning default");
            return defaultValue;
        }

        byte[] data = ServerAPI.WorldManager.SaveGame.GetData(key);

        return DataDeserialize<T>(data, defaultValue);
    }

    public static void StoreData<T>(string key, T data)
    {
        Debug.Log($"Storing data to SaveGame...");
        if (ServerAPI.WorldManager.SaveGame == null)
        {
            Debug.Log("Error: ServerAPI is null. Skipping storing data");
            return;
        }

        byte[] dataBytes = SerializerUtil.Serialize(data);

        ServerAPI.WorldManager.SaveGame.StoreData(key, dataBytes);

        Debug.Log("Data stored successfully!");
    }

    public static EnumBlockMaterial GetMaterial(Block block)
    {
        if (block is null)
            return EnumBlockMaterial.Air;
        return block.BlockMaterial;
    }

    public static int GetTier(Block block)
    {
        if (block is null)
        {
            Debug.Log("Error: Block is null. Returning 0.");
            return 0;
        }
        return block.RequiredMiningTier;
    }

    public static EnumTool GetActiveTool(IServerPlayer player)
    {
        EnumTool? usedTool = player.InventoryManager.ActiveTool;
        if (usedTool == null)
        {
            return EnumTool.Wrench;
        }
        return (EnumTool)usedTool;
    }

    public static T DataDeserialize<T>(byte[] dataBytes, T defaultValue)
    {
        Debug.Log("Deserializing data...");
        if (dataBytes == null)
        {
            Debug.Log("Deserializing Failed: Data is null.");
            return defaultValue;
        }

        T data = SerializerUtil.Deserialize<T>(dataBytes);
        if (data == null)
        {
            Debug.Log("Deserializing Failed: Deserialized Data is null.");
            return defaultValue;
        }

        Debug.Log($"Deserializing Succeeded!");
        return data;
    }

    public static Dictionary<string, int> GetSavedExperience(IServerPlayer player)
    {
        string playerSkillsKey = $"Skill_{player.PlayerUID}";
        Dictionary<string, int> playerSkills = GetData(
            playerSkillsKey,
            new Dictionary<string, int>()
        );

        Debug.Log("Retrieved player skills");
        return playerSkills;
    }

    public static void SaveExperience(IServerPlayer player, string skill, int value)
    {
        Debug.Log($"Saving experience for {player.Entity.GetName()}: {skill} => {value}");
        string playerSkillsKey = $"Skill_{player.PlayerUID}";
        Dictionary<string, int> playerSkills = GetSavedExperience(player);
        playerSkills[skill] = value;

        StoreData(playerSkillsKey, playerSkills);
    }
}

public class ShovelSkill
{
#pragma warning disable CA2211 // Non-constant fields should not be visible
    public static ICoreClientAPI ClientAPI;
#pragma warning restore CA2211 // Non-constant fields should not be visible
    public static ICoreServerAPI ServerAPI;

    public static void InitServerApi(ICoreServerAPI api)
    {
        ServerAPI = api;
    }

    public static void InitClientApi(ICoreClientAPI api)
    {
        ClientAPI = api;
    }

    public static bool ShouldGainXP(IServerPlayer player, Block block)
    {
        Debug.Log("Check if should gain xp for shovel");
        EnumTool? usedTool = Utils.GetActiveTool(player);
        EnumBlockMaterial? material = Utils.GetMaterial(block);

        if (usedTool != EnumTool.Shovel)
        {
            Debug.Log("Player is not using shovel.");
            return false;
        }

        if (material != EnumBlockMaterial.Soil)
        {
            Debug.Log("Block is not soil.");
            return false;
        }

        return true;
    }

    public static int GetXP(Block block)
    {
        Debug.Log("Calculating xp for shovel");
        if (block is null)
        {
            Debug.Log("Error: Block is null.");
            return 0;
        }

        int tier = Utils.GetTier(block);

        Debug.Log($"Block tier is {tier}. So xp is multiplied by {tier / 100}");

        return 1 + tier / 100;
    }

    public static void GainXP(IServerPlayer player, Block block)
    {
        Debug.Log("Try to gain xp for shovel...");

        if (!ShouldGainXP(player, block))
        {
            return;
        }

        if (block is null)
        {
            return;
        }
        int xp = GetXP(block);
        Debug.Log($"Gained {xp} xp for shovel");

        Dictionary<string, int> playerSkills =
            Utils.GetSavedExperience(player) ?? new Dictionary<string, int>();

        bool hasKey = playerSkills.TryGetValue("Shovel", out int oldXP);

        if (!hasKey)
        {
            Debug.Log("Creating new key. Setting xp to 0");
            oldXP = 0;
        }
        Utils.SaveExperience(player, "Shovel", oldXP + xp);
    }
}

public class Debug
{
    private static readonly OperatingSystem system = Environment.OSVersion;
    private static ILogger loggerForNonTerminalUsers;

    public static void LoadLogger(ILogger logger) => loggerForNonTerminalUsers = logger;

    public static void Log(string message)
    {
        bool TerminalUser =
            (system.Platform == PlatformID.Unix || system.Platform == PlatformID.Other)
            && Environment.UserInteractive;

        if (TerminalUser)
        {
            Console.WriteLine($"{DateTime.Now:d.M.yyyy HH:mm:ss} [ProficiencySurge] {message}");

            return;
        }

        loggerForNonTerminalUsers?.Log(EnumLogType.Notification, $"[ProficiencySurge] {message}");
    }
}
