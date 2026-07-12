using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using KillerFactory.Mechanics;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace KillerFactory;

[ModInitializer(nameof(Initialize))]
public partial class Entry
{
    // ModId 需要和 KillerFactory.json 里的 id 保持一致。
    // res://KillerFactory/... 里的 KillerFactory 是 PCK 资源目录，不是 C# namespace。
    public const string ModId = "KillerFactory";
    public const string ResPath = $"res://{ModId}";

    public static Logger Logger { get; } = new(ModId, LogType.Generic);

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 以下示例默认已经在 Entry.Initialize() 中调用了
        // RitsuLibFramework.EnsureGodotScriptsRegistered(...) 和
        // ModTypeDiscoveryHub.RegisterModAssembly(...)，否则自动注册不会生效。
        //
        // Godot C# 脚本注册只负责让 pck 中的脚本类型能被 Godot 找到。
        // 这一步和 RitsuLib 的内容自动注册不是同一件事，两个都需要保留。
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);

        // 自动注册扫描会读取当前程序集里的 RegisterCard/RegisterRelic 等 attribute。
        // 新增内容类后，只要 attribute 写对，通常不需要在入口里手动逐个注册。
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);
        new Harmony("killer_factory.runtime").PatchAll(assembly);

        // 产线面板只应在当前战斗由初始遗物激活，避免上一场状态泄漏到其他角色或战斗。
        RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(static _ => FactoryCombatState.ClearCurrent());
        RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(static _ => FactoryCombatState.ClearCurrent());

        Logger.Info("KillerFactory initialized.");
    }
}
