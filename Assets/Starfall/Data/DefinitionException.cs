using System;

namespace Starfall.Data
{
    public class DefinitionException : Exception
    {
        public string FilePath { get; }
        public string FieldPath { get; }
        public object? Value { get; }

        public DefinitionException(string message, string filePath, string fieldPath, object? value, Exception? inner = null)
            : base($"[{filePath}] {fieldPath}={value ?? "null"}: {message}", inner)
        {
            FilePath = filePath;
            FieldPath = fieldPath;
            Value = value;
        }
    }
}