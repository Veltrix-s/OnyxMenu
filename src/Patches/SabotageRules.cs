using HarmonyLib;
using Hazel;

namespace Nocturne;

internal static class SabotageRules
{
    internal static bool AnyBlocked => NocturneConfig.BlockLights.Value || NocturneConfig.BlockReactor.Value
        || NocturneConfig.BlockLaboratory.Value || NocturneConfig.BlockOxygen.Value
        || NocturneConfig.BlockComms.Value || NocturneConfig.BlockHeli.Value
        || NocturneConfig.BlockMushroom.Value;

    internal static bool Blocked(SystemTypes sys)
    {
        return sys switch
        {
            SystemTypes.Electrical => NocturneConfig.BlockLights.Value,
            SystemTypes.Reactor => NocturneConfig.BlockReactor.Value,
            SystemTypes.Laboratory => NocturneConfig.BlockLaboratory.Value,
            SystemTypes.LifeSupp => NocturneConfig.BlockOxygen.Value,
            SystemTypes.Comms => NocturneConfig.BlockComms.Value,
            SystemTypes.HeliSabotage => NocturneConfig.BlockHeli.Value,
            SystemTypes.MushroomMixupSabotage => NocturneConfig.BlockMushroom.Value,
            _ => false
        };
    }
}

[HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.UpdateSystem))]
internal static class SabotageRulesBlockPatch
{
    public static bool Prefix([HarmonyArgument(1)] MessageReader reader)
    {
        if (!Utils.Host || !SabotageRules.AnyBlocked)
            return true;

        MessageReader copy = null;
        try
        {
            copy = MessageReader.Get(reader);
            return !SabotageRules.Blocked((SystemTypes)copy.ReadByte());
        }
        catch
        {
            return true;
        }
        finally
        {
            if (copy != null)
                copy.Recycle();
        }
    }
}
