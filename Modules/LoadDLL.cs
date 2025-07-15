using System;
using System.Reflection;
using System.Collections.Generic;

namespace TownOfHostForE.Modules
{
    internal class LoadDLL
    {
        //キャッシュ
        private static readonly Dictionary<string, Assembly> _loadedAssemblies = new();

        public static void OnLoadDLL()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var requestedAssemblyName = new AssemblyName(args.Name).Name;

            // すでにロード済みなら返す
            if (_loadedAssemblies.TryGetValue(requestedAssemblyName, out var loadedAssembly))
                return loadedAssembly;

            // 埋め込みリソース名を作る（例: "YourNamespace.Libs.LibraryA.dll"）
            var currentAssembly = Assembly.GetExecutingAssembly();
            string resourceName = FindResourceName(currentAssembly, requestedAssemblyName);

            if (resourceName == null)
                return null;

            using var stream = currentAssembly.GetManifestResourceStream(resourceName);
            if (stream == null) return null;

            byte[] assemblyData = new byte[stream.Length];
            stream.Read(assemblyData, 0, assemblyData.Length);
            var assembly = Assembly.Load(assemblyData);

            _loadedAssemblies[requestedAssemblyName] = assembly; // キャッシュ
            return assembly;
        }

        // リソース名からDLL名が一致するものを探す
        private static string FindResourceName(Assembly assembly, string requestedDllName)
        {
            foreach (var resource in assembly.GetManifestResourceNames())
            {
                if (resource.EndsWith(requestedDllName + ".dll", StringComparison.OrdinalIgnoreCase))
                    return resource;
            }
            return null;
        }
    }
}
