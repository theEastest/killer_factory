using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;

namespace KillerFactory.Mechanics;

public static class FactoryCombatEndWatcher
{
    private static int _checkQueued;

    public static void OnCardMoved()
    {
        var manager = CombatManager.Instance;
        if (!manager.IsEnding || Interlocked.Exchange(ref _checkQueued, 1) != 0)
            return;
        TaskHelper.RunSafely(CheckAsync(manager));
    }

    private static async Task CheckAsync(CombatManager manager)
    {
        try
        {
            // 让当前牌的移牌命令和 OnPlayWrapper 完成收尾，再进入胜利流程。
            await Task.Yield();
            await manager.CheckWinCondition();
        }
        finally
        {
            Interlocked.Exchange(ref _checkQueued, 0);
        }
    }
}
