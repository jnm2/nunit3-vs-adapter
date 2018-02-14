// ***********************************************************************
// Copyright (c) 2018 Charlie Poole, Terje Sandstrom
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

#if !NET35
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using NUnit.VisualStudio.TestAdapter.Metadata.MetadataReaderHelpers;

namespace NUnit.VisualStudio.TestAdapter.Metadata
{
    internal sealed class MetadataReaderMetadataProvider : IMetadataProvider
    {
        private readonly Dictionary<string, PEReader> _readersByAssemblyPath = new Dictionary<string, PEReader>(StringComparer.OrdinalIgnoreCase);

        private MetadataReader GetReader(string assemblyPath)
        {
            if (!_readersByAssemblyPath.TryGetValue(assemblyPath, out var peReader))
                _readersByAssemblyPath.Add(assemblyPath, peReader = new PEReader(File.OpenRead(assemblyPath)));

            return peReader.GetMetadataReader();
        }

        public void Dispose()
        {
            foreach (var session in _readersByAssemblyPath.Values)
                session.Dispose();
        }

        public TypeResult? GetDeclaringType(string assemblyPath, string reflectedTypeName, string methodName)
        {
            var currentReader = GetReader(assemblyPath);
            var currentAssemblyPath = assemblyPath;
            var currentType = MetadataReaderUtils.FindType(currentReader, reflectedTypeName);
            if (currentType == null) return null;

            var candidate = (TypeResult?)null;
            var isSameType = true;

            for (;;)
            {
                foreach (var methodHandle in currentType.Value.GetMethods())
                {
                    var method = currentReader.GetMethodDefinition(methodHandle);
                    if ((method.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public) continue;
                    if (currentReader.GetString(method.Name) != methodName) continue;

                    if (candidate != null) return null;
                    candidate = new TypeResult(currentAssemblyPath, isSameType
                        ? reflectedTypeName
                        : MetadataReaderUtils.GetTypeFullName(currentReader, currentType.Value));
                }

                isSameType = false;
                if (currentType.Value.BaseType.IsNil) break;

                var resolved = MetadataReaderResolution.ResolveType(
                    currentReader,
                    currentType.Value.BaseType,
                    simpleName =>
                        FindAssemblyInDirectory(simpleName, Path.GetDirectoryName(currentAssemblyPath)));

                if (resolved == null) break;

                currentType = resolved.Value.TypeDefinition;
                if (resolved.Value.ExternalAssembly != null)
                {
                    currentAssemblyPath = resolved.Value.ExternalAssembly.Value.AssemblyPath;
                    currentReader = resolved.Value.ExternalAssembly.Value.Reader;
                }
            }

            return candidate;
        }

        private ResolvedAssembly? FindAssemblyInDirectory(string simpleName, string directory)
        {
            var basePath = Path.Combine(directory, simpleName);
            return TryOpenAssemblyInDirectory(basePath, ".dll")
                ?? TryOpenAssemblyInDirectory(basePath, ".exe");
        }

        private ResolvedAssembly? TryOpenAssemblyInDirectory(string assemblyPath, string extension)
        {
            assemblyPath = Path.Combine(assemblyPath, Path.ChangeExtension(assemblyPath, extension));
            try
            {
                return new ResolvedAssembly(assemblyPath, GetReader(assemblyPath));
            }
            catch (FileNotFoundException) // Only race-condition-proof API .NET has
            {
            }

            return null;
        }

        public TypeResult? GetStateMachineType(string assemblyPath, string reflectedTypeName, string methodName)
        {
            return null;
        }
    }
}
#endif
