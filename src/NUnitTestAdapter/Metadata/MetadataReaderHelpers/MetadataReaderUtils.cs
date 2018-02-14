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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Text;

namespace NUnit.VisualStudio.TestAdapter.Metadata.MetadataReaderHelpers
{
    internal static class MetadataReaderUtils
    {
        #region FindType for full name string

        public static TypeDefinition? FindType(MetadataReader reader, string fullName)
        {
            return FindType(reader, new FullNameTypeNameEnumerator(fullName));
        }

        private struct FullNameTypeNameEnumerator : ITypeNameEnumerator
        {
            private readonly string fullName;
            private int position;

            public FullNameTypeNameEnumerator(string fullName)
            {
                this.fullName = fullName;
                position = 0;
            }

            public string TryGetNextNamespaceSegment()
            {
                var nextSegmentEnd = fullName.IndexOf('.', position);
                if (nextSegmentEnd == -1) return null;
                var segment = fullName.Substring(position, nextSegmentEnd - position);
                position = nextSegmentEnd + 1;
                return segment;
            }

            public string TryGetNextContainingTypeName()
            {
                var nextContainingTypeEnd = fullName.IndexOf('+', position);
                if (nextContainingTypeEnd == -1) return null;
                var containingType = fullName.Substring(position, nextContainingTypeEnd - position);
                position = nextContainingTypeEnd + 1;
                return containingType;
            }

            public string GetFinalName()
            {
                return fullName.Substring(position);
            }
        }

        #endregion

        #region FindType for name parts

        public static TypeDefinition? FindType(MetadataReader reader, string @namespace, ImmutableArray<string> containingTypeNames, string finalName)
        {
            return FindType(reader, new NamePartsTypeNameEnumerator(@namespace, containingTypeNames, finalName));
        }

        private struct NamePartsTypeNameEnumerator : ITypeNameEnumerator
        {
            private readonly string @namespace;
            private int namespacePosition;
            private ImmutableArray<string>.Enumerator containingTypeNames;
            private readonly string finalName;

            public NamePartsTypeNameEnumerator(string @namespace, ImmutableArray<string> containingTypeNames, string finalName)
            {
                this.@namespace = @namespace;
                this.containingTypeNames = containingTypeNames.GetEnumerator();
                this.finalName = finalName;
                namespacePosition = 0;
            }

            public string TryGetNextNamespaceSegment()
            {
                if (namespacePosition == -1) return null;
                var nextSegmentEnd = @namespace.IndexOf('.', namespacePosition);
                if (nextSegmentEnd == -1)
                {
                    var segment = @namespace.Substring(namespacePosition);
                    namespacePosition = -1;
                    return segment;
                }
                else
                {
                    var segment = @namespace.Substring(namespacePosition, nextSegmentEnd - namespacePosition);
                    namespacePosition = nextSegmentEnd + 1;
                    return segment;
                }
            }

            public string TryGetNextContainingTypeName()
            {
                return containingTypeNames.MoveNext() ? containingTypeNames.Current : null;
            }

            public string GetFinalName()
            {
                return finalName;
            }
        }

        #endregion

        #region FindType implementation

        private interface ITypeNameEnumerator
        {
            string TryGetNextNamespaceSegment();

            /// <summary>
            /// May only be called after <see cref="TryGetNextNamespaceSegment"/> has returned <see langword="null"/>.
            /// </summary>
            string TryGetNextContainingTypeName();

            /// <summary>
            /// May only be called after <see cref="TryGetNextContainingTypeName"/> has returned <see langword="null"/>.
            /// </summary>
            string GetFinalName();
        }

        private static TypeDefinition? FindType<T>(MetadataReader reader, T fullName) where T : ITypeNameEnumerator
        {
            var currentContainer = reader.GetNamespaceDefinitionRoot();

            for (; ; )
            {
                var segment = fullName.TryGetNextNamespaceSegment();
                if (segment == null) break;
                var found = false;

                foreach (var handle in currentContainer.NamespaceDefinitions)
                {
                    var ns = reader.GetNamespaceDefinition(handle);
                    if (reader.GetString(ns.Name) == segment)
                    {
                        currentContainer = ns;
                        found = true;
                        break;
                    }
                }

                if (!found) return null;
            }

            var currentTypeArray = currentContainer.TypeDefinitions;

            for (; ; )
            {
                var containingType = fullName.TryGetNextContainingTypeName();
                var nextTypeName = containingType ?? fullName.GetFinalName();

                var found = false;

                foreach (var handle in currentTypeArray)
                {
                    var type = reader.GetTypeDefinition(handle);
                    if (reader.GetString(type.Name) == nextTypeName)
                    {
                        if (containingType == null) return type;
                        currentTypeArray = type.GetNestedTypes();
                        found = true;
                        break;
                    }
                }

                if (!found) return null;
            }
        }

        #endregion

        public static string GetTypeFullName(MetadataReader reader, TypeDefinition definition)
        {
            var nestedTypeNames = new List<StringHandle>();

            var currentType = definition;

            for (;;)
            {
                var handle = currentType.GetDeclaringType();
                if (handle.IsNil) break;
                nestedTypeNames.Add(currentType.Name);
                currentType = reader.GetTypeDefinition(handle);
            }

            var sb = new StringBuilder()
                .Append(reader.GetString(currentType.Namespace))
                .Append('.').Append(reader.GetString(currentType.Name));

            for (var i = nestedTypeNames.Count - 1; i >= 0; i--)
                sb.Append('+').Append(reader.GetString(nestedTypeNames[i]));

            return sb.ToString();
        }
    }
}
#endif
