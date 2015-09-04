﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Xunit.Performance;
using Xunit;

namespace SimplePerfTests
{
    public class Document
    {
        private string _text;

        public Document(string text)
        {
            _text = text;
        }

        public void Format()
        {
            _text = _text.ToUpper();
        }

        public override string ToString()
        {
            return _text;
        }
    }

    public class FormattingTests
    {
        private static IEnumerable<object[]> MakeArgs(params object[] args)
        {
            return args.Select(arg => new object[] { arg });
        }

        public static IEnumerable<object[]> FormatCurlyBracesMemberData = MakeArgs(
            new Document("Hello, world!")
        );

        [Benchmark]
        [MemberData(nameof(FormatCurlyBracesMemberData))]
        public static void FormatCurlyBracesTest(Document document)
        {
            foreach (var iteration in Benchmark.Iterations)
                using (iteration.StartMeasurement())
                    document.Format();
        }
    }
}