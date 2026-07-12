using UnityEngine;
using Starfall.Core.Combat;
using Starfall.Core.Model;
using Starfall.Data;
using Starfall.Data.Definition;
using Starfall.Data.Loading;
using Starfall.Data.Validation;

namespace Starfall.Unity
{
    /// <summary>
    /// Unity 入口：从 Resources/data/*.json 加载 BattleDefinition，构建 BattleState，
    /// 创建 BattleRunner（默认 SimpleEnemyAI），挂上 BoardPresenter + BattleHud。
    /// 不持有 BattleState 引用——只持有 BattleRunner。
    /// </summary>
    public class BattleBootstrap : MonoBehaviour
    {
        [SerializeField] private string _battleDefinitionPath = "data/battle_default";
        [SerializeField] private MonoBehaviour _boardPresenter;  // 实现 IBoardPresenter
        [SerializeField] private MonoBehaviour _battleHud;       // 实现 IBattleHud

        public BattleRunner Runner { get; private set; }

        private void Awake()
        {
            var def = LoadDefinition(_battleDefinitionPath);
            DefinitionValidator.Validate(def, _battleDefinitionPath);
            var state = BattleStateBuilder.Build(def);
            Runner = new BattleRunner(state);
        }

        private void Start()
        {
            // Presenter / HUD 在 Awake 后通过接口拿快照
            if (Runner == null) return;
            var boardPresenter = _boardPresenter as Presentation.IBoardPresenter;
            var battleHud = _battleHud as Presentation.IBattleHud;
            // 首次渲染：snapshot 来自 Runner.State
            if (boardPresenter != null)
                boardPresenter.Render(
                    Presentation.BoardSnapshot.FromState(Runner.State),
                    System.Array.Empty<Presentation.PresentationEvent>());
            if (battleHud != null)
                battleHud.Render(
                    Presentation.HudSnapshot.FromState(Runner.State, Runner.Outcome),
                    System.Array.Empty<Presentation.PresentationEvent>());
        }

        private static BattleDefinition LoadDefinition(string path)
        {
            var fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, path + ".json");
            return JsonBattleLoader.Load(fullPath);
        }
    }
}