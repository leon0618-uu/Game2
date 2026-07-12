using UnityEngine;
using Starfall.Unity.Presentation;

namespace Starfall.Unity
{
    /// <summary>
    /// 占位棋盘表现：把快照与事件打到 Unity Console。MVP 替代 sprite/UI 渲染。
    /// </summary>
    public class StubBoardPresenter : MonoBehaviour, IBoardPresenter
    {
        public void Render(in BoardSnapshot snapshot, in System.Collections.Generic.IReadOnlyList<PresentationEvent> events)
        {
            Debug.Log($"[StubBoardPresenter] Board snapshot W={snapshot.Width} H={snapshot.Height} Tiles={snapshot.Tiles.Count} Events={events.Count}");
        }
    }
}