using Game.NetWork;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// SCNET x25.01.17 authority gate. Any failure to establish the role is a
/// refusal to mutate world state.
/// </summary>
public static class RuntimeCompat
{
    public static bool CanMutateWorld
    {
        get
        {
            try
            {
                return IsAuthoritativeRole(CommonLib.WorkType, CommonLib.IsWorkTypeValid());
            }
            catch (Exception exception)
            {
                Engine.Log.Warning($"[Ruins] Cannot determine SCNET authority; world writes disabled: {exception.Message}");
                return false;
            }
        }
    }

    public static bool IsAuthoritativeRole(WorkType role, bool roleIsValid)
    {
        return roleIsValid && (role == WorkType.Local || role == WorkType.Server);
    }
}
