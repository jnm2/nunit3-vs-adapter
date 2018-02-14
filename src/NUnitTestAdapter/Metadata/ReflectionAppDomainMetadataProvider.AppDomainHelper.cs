﻿// ***********************************************************************
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

#if NET35
using System;
using System.Linq;
using System.Reflection;

namespace NUnit.VisualStudio.TestAdapter.Metadata
{
    partial class ReflectionAppDomainMetadataProvider
    {
        private sealed class AppDomainHelper : MarshalByRefObject
        {
            public TypeResult? GetDeclaringType(string assemblyPath, string reflectedTypeName, string methodName)
            {
                var type = TryGetSingleMethod(assemblyPath, reflectedTypeName, methodName)?.DeclaringType;
                if (type == null) return null;
                return new TypeResult(type);
            }

            public TypeResult? GetStateMachineType(string assemblyPath, string reflectedTypeName, string methodName)
            {
                var method = TryGetSingleMethod(assemblyPath, reflectedTypeName, methodName);
                if (method == null) return null;

                var candidate = (Type)null;

                foreach (var attributeData in CustomAttributeData.GetCustomAttributes(method))
                {
                    for (var current = attributeData.Constructor.DeclaringType; current != null; current = current.BaseType)
                    {
                        if (current.FullName != "System.Runtime.CompilerServices.StateMachineAttribute") continue;

                        var parameters = attributeData.Constructor.GetParameters();
                        for (var i = 0; i < parameters.Length; i++)
                        {
                            if (parameters[i].Name != "stateMachineType") continue;
                            var argument = attributeData.ConstructorArguments[i].Value as Type;
                            if (argument != null)
                            {
                                if (candidate != null) return null;
                                candidate = argument;
                            }
                        }
                    }
                }

                if (candidate == null) return null;
                return new TypeResult(candidate);
            }

            private static MethodInfo TryGetSingleMethod(string assemblyPath, string reflectedTypeName, string methodName)
            {
                var type = Assembly.ReflectionOnlyLoadFrom(assemblyPath).GetType(reflectedTypeName, throwOnError: false);
                if (type == null) return null;

                var methods = type.GetMethods().Where(m => m.Name == methodName).Take(2).ToList();
                return methods.Count == 1 ? methods[0] : null;
            }
        }
    }
}
#endif
