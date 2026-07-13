namespace Starfall.Core.Map.Coordinates
{
    /// <summary>
    /// 维度层（doc2 MAP-01 §4.3）。
    /// 同 (X, Y) 不同 Layer 视为不同地块；Reality 与 Astral 共用相同的 X/Y 范围，
    /// 但内容（地形 / 锚点 / 单位 / 律令 CV）相互独立。
    ///
    /// 数值顺序固定：Reality = 0, Astral = 1。
    /// 任何按 Layer 排序的逻辑都必须遵守此顺序，禁止硬编码 magic number。
    /// </summary>
    public enum DimensionLayer : byte
    {
        Reality = 0,
        Astral = 1,
    }
}