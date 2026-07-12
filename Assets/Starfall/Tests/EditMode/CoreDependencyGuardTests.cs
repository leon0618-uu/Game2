using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Starfall.Tests.EditMode
{
    /// <summary>
    /// Core 程序集依赖守卫。
    /// 4 项核心检查：asmdef 文本 / 编译引用 / MonoBehaviour / ScriptableObject。
    /// 任何一项失败 = Starfall.Core 已被 Unity 污染 = AGENTS.md §10.1 硬约束违反。
    /// </summary>
    public class CoreDependencyGuardTests
    {
        private const string CoreAsmdefRelPath = "Starfall/Core/Starfall.Core.asmdef";
        private const string CoreAssemblyName = "Starfall.Core";

        private static Assembly FindCoreAssembly()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == CoreAssemblyName);
        }

        [Test]
        public void Core_Asmdef_DoesNotReferenceUnity()
        {
            var asmdefPath = Path.Combine(Application.dataPath, CoreAsmdefRelPath);
            Assert.IsTrue(File.Exists(asmdefPath), $"Starfall.Core.asmdef missing at {asmdefPath}");

            var json = File.ReadAllText(asmdefPath);
            Assert.IsFalse(json.Contains("UnityEngine"), "Starfall.Core.asmdef must not contain 'UnityEngine'");
            Assert.IsFalse(json.Contains("UnityEditor"), "Starfall.Core.asmdef must not contain 'UnityEditor'");
            Assert.IsTrue(json.Contains("\"noEngineReferences\": true"),
                "Starfall.Core must declare noEngineReferences: true to block implicit Unity engine references");
        }

        [Test]
        public void Core_NoUnityAssemblyRefs()
        {
            var coreAsm = FindCoreAssembly();
            Assert.IsNotNull(coreAsm, "Starfall.Core assembly not loaded");

            foreach (var r in coreAsm.GetReferencedAssemblies())
            {
                Assert.IsFalse(r.Name.StartsWith("UnityEngine"),
                    $"Starfall.Core has forbidden UnityEngine reference: {r.Name}");
                Assert.IsFalse(r.Name.StartsWith("UnityEditor"),
                    $"Starfall.Core has forbidden UnityEditor reference: {r.Name}");
            }
        }

        [Test]
        public void Core_NoMonoBehaviourSubclasses()
        {
            var coreAsm = FindCoreAssembly();
            Assert.IsNotNull(coreAsm, "Starfall.Core assembly not loaded");

            foreach (var type in coreAsm.GetTypes())
            {
                Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(type),
                    $"Starfall.Core type {type.FullName} inherits MonoBehaviour (forbidden by AGENTS.md §10.1)");
            }
        }

        [Test]
        public void Core_NoScriptableObjectSubclasses()
        {
            var coreAsm = FindCoreAssembly();
            Assert.IsNotNull(coreAsm, "Starfall.Core assembly not loaded");

            foreach (var type in coreAsm.GetTypes())
            {
                Assert.IsFalse(typeof(ScriptableObject).IsAssignableFrom(type),
                    $"Starfall.Core type {type.FullName} inherits ScriptableObject (forbidden by AGENTS.md §10.1)");
            }
        }
    }
}