﻿using System.Collections.Generic;
using Elympics;
using NUnit.Framework;

namespace Tests
{
	public class TestEqualityComparers
	{
		[Test]
		public void TestNegativeToleranceBehavesLikeZeroTolerance()
		{
			var comparer = new ElympicsFloatEqualityComparer(-10f);
			Assert.IsTrue(comparer.Equals(0f, 0f));
			Assert.IsFalse(comparer.Equals(0f, float.Epsilon));
		}

		#region ElympicsIntEqualityComparer tests

		private static IEnumerable<EqualityComparerTestCase<int>> IntTestCasesWithToleranceTooLow => new List<EqualityComparerTestCase<int>>
		{
			new EqualityComparerTestCase<int>(0, 1, 0f),
			new EqualityComparerTestCase<int>(int.MinValue, 0, 10e5f),
			new EqualityComparerTestCase<int>(0, int.MinValue, 10e5f),
			new EqualityComparerTestCase<int>(int.MinValue, int.MaxValue, 10e5f)
		};

		private static IEnumerable<EqualityComparerTestCase<int>> IntTestCasesWithToleranceSufficient => new List<EqualityComparerTestCase<int>>
		{
			new EqualityComparerTestCase<int>(0, 0, 0f),
			new EqualityComparerTestCase<int>(0, 0, 100f),  // (much) greater than needed
			new EqualityComparerTestCase<int>(0, 1, 1f),
			new EqualityComparerTestCase<int>(int.MinValue, 0, -(float)int.MinValue),
			new EqualityComparerTestCase<int>(0, int.MinValue, -(float)int.MinValue),
			new EqualityComparerTestCase<int>(int.MinValue, int.MaxValue, -(float)int.MinValue * 2)
		};

		[Test]
		public void TestElympicsIntEqualityComparerWithToleranceTooLow([ValueSource(nameof(IntTestCasesWithToleranceTooLow))] EqualityComparerTestCase<int> testCase)
		{
			var comparer = new ElympicsIntEqualityComparer(testCase.Tolerance);
			Assert.IsFalse(comparer.Equals(testCase.Left, testCase.Right));
		}

		[Test]
		public void TestElympicsIntEqualityComparerWithToleranceSufficient([ValueSource(nameof(IntTestCasesWithToleranceSufficient))] EqualityComparerTestCase<int> testCase)
		{
			var comparer = new ElympicsIntEqualityComparer(testCase.Tolerance);
			Assert.IsTrue(comparer.Equals(testCase.Left, testCase.Right));
		}

		#endregion ElympicsIntEqualityComparer tests

		public class EqualityComparerTestCase<T>
		{
			public T Left;
			public T Right;
			public float Tolerance;

			public EqualityComparerTestCase(T a, T b, float tolerance)
			{
				(Left, Right, Tolerance) = (a, b, tolerance);
			}
		}
	}
}
