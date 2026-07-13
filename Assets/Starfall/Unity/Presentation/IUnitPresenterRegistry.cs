using Starfall.Core.Model;

namespace Starfall.Unity.Presentation
{
    public interface IUnitPresenter
    {
        void Render(in UnitSnapshot snapshot);
        void Dispose();
    }

    public interface IUnitPresenterRegistry
    {
        void Register(UnitIdKey key, IUnitPresenter presenter);
        IUnitPresenter Resolve(UnitIdKey key);
    }

    /// <summary>
    /// 用 UnitId 作为键的轻量包装（避免直接 int 键与 Presenter 内部冲突）。
    /// </summary>
    public readonly struct UnitIdKey
    {
        public int Value { get; }
        public UnitIdKey(int value) { Value = value; }
        public override int GetHashCode() => Value;
        public override bool Equals(object obj) => obj is UnitIdKey k && k.Value == Value;
    }
}