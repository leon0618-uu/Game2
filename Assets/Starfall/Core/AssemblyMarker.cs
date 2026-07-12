namespace Starfall.Core
{
    /// <summary>
    /// Pure assembly anchor. Exists solely to ensure Starfall.Core.dll is
    /// compiled by Unity even when no other Core types are present (e.g.
    /// during Task 02 skeleton stage). Reflection-based guard tests in
    /// Starfall.Tests.EditMode locate "Starfall.Core" assembly via this type.
    /// This file must contain no business logic.
    /// </summary>
    internal static class AssemblyMarker
    {
    }
}