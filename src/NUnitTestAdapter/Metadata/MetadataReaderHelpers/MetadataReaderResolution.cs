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
using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace NUnit.VisualStudio.TestAdapter.Metadata.MetadataReaderHelpers
{
    internal static class MetadataReaderResolution
    {
        public static TypeResolutionResult? ResolveType(MetadataReader reader, EntityHandle handle, Func<string, ResolvedAssembly?> resolveAssemblyBySimpleName)
        {
            if (handle.IsNil) return null;

            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    return new TypeResolutionResult(null, reader.GetTypeDefinition((TypeDefinitionHandle)handle));

                case HandleKind.TypeReference:
                    var currentTypeReference = reader.GetTypeReference((TypeReferenceHandle)handle);

                    var @namespace = reader.GetString(currentTypeReference.Namespace);
                    var containingNames = ImmutableArray.CreateBuilder<string>();
                    var finalName = reader.GetString(currentTypeReference.Name);

                    while (currentTypeReference.ResolutionScope.Kind == HandleKind.TypeReference)
                    {
                        currentTypeReference = reader.GetTypeReference((TypeReferenceHandle)currentTypeReference.ResolutionScope);
                        containingNames.Add(reader.GetString(currentTypeReference.Name));
                    }

                    if (currentTypeReference.ResolutionScope.Kind != HandleKind.AssemblyReference)
                        return null;

                    var assemblyReference = reader.GetAssemblyReference((AssemblyReferenceHandle)currentTypeReference.ResolutionScope);
                    var resolvedAssembly = resolveAssemblyBySimpleName.Invoke(reader.GetString(assemblyReference.Name));
                    if (resolvedAssembly == null) return null;

                    var newReader = resolvedAssembly.Value.Reader;

                    var definition = MetadataReaderUtils.FindType(newReader, @namespace, containingNames.MoveToImmutable(), finalName);
                    if (definition == null) return null;

                    return new TypeResolutionResult(resolvedAssembly, definition.Value);

                case HandleKind.TypeSpecification:
                    // TODO: tests
                    throw new NotImplementedException();

                default:
                    return null;
            }
        }
    }
}
#endif
