using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 设置地图调试值命令（test-only）。
    /// <para/>
    /// **用途**：测试场景下写入一个 key→value 字符串到 <see cref="MapState"/> 的
    /// 调试存储（外部 <c>MapDevTest</c> 字典）。<strong>不允许在生产路径使用</strong>。
    /// <para/>
    /// **强制门槛**：<see cref="MapState"/> 必须先用
    /// <see cref="MapState.SetDevTestMode"/>
    /// 开启（实现在 <see cref="MapState"/> 中检测一个布尔字段）；未开启时一律返回
    /// <c>"map dev test mode not enabled"</c>。这是 AGENTS.md §10.1 "test-only 隔离" 策略
    /// 的实施点。
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnMapDebugValueChanged"/> 事件（含 key）。
    /// </summary>
    public sealed class SetMapDebugValueCommand : IMapCommand
    {
        public string Key { get; }
        public string Value { get; }

        public SetMapDebugValueCommand(string key, string value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (value == null) throw new ArgumentNullException(nameof(value));
            Key = key;
            Value = value;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            if (!mapState.DevTestModeEnabled)
            {
                // 默认禁用；调用方须先 mapState.EnableDevTestMode()。
                return MapCommandResult.Fail("map dev test mode not enabled");
            }

            _previousValue = mapState.TryGetDebugValue(Key);
            mapState.SetDebugValue(Key, Value);
            _executed = true;

            var events = new List<MapEvent>(1)
            {
                MapEvent.MapDebugValueChanged(Key, $"debug-value:{Key}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("SetMapDebugValueCommand.Undo called without prior Execute.");
            if (_previousValue != null)
                mapState.SetDebugValue(Key, _previousValue);
            else
                mapState.RemoveDebugValue(Key);
            _executed = false;
        }

        public int Version => 1;
        public string CommandId => $"set-map-debug-value:{Key}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private string _previousValue;

        public override string ToString()
            => $"SetMapDebugValueCommand(Key={Key}, Value={Value})";
    }
}
