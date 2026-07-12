using System.Collections.Generic;

namespace Starfall.Unity.Presentation
{
    /// <summary>
    /// HUD 渲染接口。订阅 BattleEvent → 内部生成 PresentationEvent → 更新 UI。
    /// </summary>
    public interface IBattleHud
    {
        void Render(in HudSnapshot snapshot, in IReadOnlyList<PresentationEvent> events);
    }
}