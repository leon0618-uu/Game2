using System.Collections.Generic;

namespace Starfall.Unity.Presentation
{
    /// <summary>
    /// 棋盘渲染接口。订阅 BattleEvent → 内部生成 PresentationEvent → 调用具体表现实现。
    /// Presenter 不持有 BattleState（不存第二真值）。
    /// </summary>
    public interface IBoardPresenter
    {
        void Render(in BoardSnapshot snapshot, in IReadOnlyList<PresentationEvent> events);
    }
}