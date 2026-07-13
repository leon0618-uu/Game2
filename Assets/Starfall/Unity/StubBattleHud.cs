using UnityEngine;
using Starfall.Unity.Presentation;

namespace Starfall.Unity
{
    /// <summary>
    /// 占位 HUD 表现：把快照与事件打到 Unity Console。
    /// </summary>
    public class StubBattleHud : MonoBehaviour, IBattleHud
    {
        public void Render(in HudSnapshot snapshot, in System.Collections.Generic.IReadOnlyList<PresentationEvent> events)
        {
            Debug.Log($"[StubBattleHud] Hud TN={snapshot.TurnNumber} AP={snapshot.ActivePlayer} Outcome={snapshot.Outcome} Events={events.Count}");
        }
    }
}