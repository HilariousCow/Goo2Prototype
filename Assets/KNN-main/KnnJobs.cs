﻿using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnsafeUtilityEx = KNN.Internal.UnsafeUtilityEx;

namespace KNN.Jobs {
	[BurstCompile(CompileSynchronously = true)]
	public struct QueryKNearestJob : IJob {
		[ReadOnly] KnnContainer m_container;
		[WriteOnly] NativeSlice<int> m_result;

		float3 m_queryPosition;

		public QueryKNearestJob(KnnContainer container, float3 queryPosition, NativeSlice<int> result) {
			m_result = result;
			m_queryPosition = queryPosition;
			m_container = container;
		}

		void IJob.Execute() {
			m_container.QueryKNearest(m_queryPosition, m_result);
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	public struct QueryRangeJob : IJob {
		[ReadOnly] KnnContainer m_container;
		[WriteOnly] NativeList<int> m_result;

		float m_range;
		float3 m_queryPosition;

		public QueryRangeJob(KnnContainer container, float3 queryPosition, float range, NativeList<int> result) {
			m_result = result;
			m_range = range;
			m_queryPosition = queryPosition;
			m_container = container;
		}

		void IJob.Execute() {
			m_container.QueryRange(m_queryPosition, m_range, m_result);
		}
	}
	
	[BurstCompile(CompileSynchronously = true)]
	public struct QueryKNearestBatchJob : IJobParallelForBatch {
		[ReadOnly] KnnContainer m_container;
		[ReadOnly] NativeSlice<float3> m_queryPositions;

		// Unity really doesn't like it when we write to the same underlying array
		// Even if slices don't overlap... So we're just being dangerous here
		[NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction]
		NativeSlice<int> m_results;

		int m_k;

		public QueryKNearestBatchJob(KnnContainer container, NativeArray<float3> queryPositions, NativeSlice<int> results) {
			m_container = container;
			m_queryPositions = queryPositions;
			m_results = results;

		#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (queryPositions.Length == 0 || results.Length % queryPositions.Length != 0) {
				Debug.LogError("Make sure your results array is a multiple in length of your querypositions array!");
			}
		#endif

			m_k = results.Length / queryPositions.Length;
		}

		public void Execute(int startIndex, int count) {
			// Write results to proper slice!
			for (int index = startIndex; index < startIndex + count; ++index) {
				NativeSlice<int> resultsSlice = m_results.Slice(index * m_k, m_k);
				m_container.QueryKNearest(m_queryPositions[index], resultsSlice);
			}
		}
	}

	public unsafe struct RangeQueryResult {
		public int Length;

		int* m_indices;
		public int m_capacity { get; private set; }

		Allocator m_allocator;

		public int this[int index] {
			get {
				if (index >= m_capacity) {
					throw new IndexOutOfRangeException();
				}

				return UnsafeUtility.ReadArrayElement<int>(m_indices, index);
			}
		}

		public RangeQueryResult(int maxCount, Allocator allocator) {
			m_capacity = maxCount;
			m_indices = UnsafeUtilityEx.AllocArray<int>(m_capacity, allocator);
			Length = 0;
			m_allocator = allocator;
		}

		public void SetResults(NativeList<int> result) {
			UnsafeUtility.MemCpy(m_indices, result.GetUnsafePtr(), Mathf.Min(m_capacity, result.Length) * sizeof(int));
			Length = Mathf.Min(m_capacity, result.Length);
		}

		public void Dispose() {
			UnsafeUtility.Free(m_indices, m_allocator);
		}
	}


	[BurstCompile(CompileSynchronously = true)]
	public struct QueryRangeBatchJob : IJobParallelFor {
		[ReadOnly] public KnnContainer m_container;
		[ReadOnly] public NativeSlice<float3> m_queryPositions;

		public float m_range;

		public NativeArray<RangeQueryResult> Results;

		public QueryRangeBatchJob(KnnContainer container, NativeArray<float3> queryPositions, float range, NativeArray<RangeQueryResult> results) {
			m_container = container;
			m_queryPositions = queryPositions;
			m_range = range;
			Results = results;
		}

		public void Execute(int index) {
			// Write results to proper slice!
		
			var tempList = new NativeList<int>(Allocator.Temp);
			m_container.QueryRange(m_queryPositions[index], m_range, tempList);

			var result = Results[index];
			result.SetResults(tempList);

			Results[index] = result;
			
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	public struct QueryMultiRangesBatchJob : IJobParallelFor {
		[ReadOnly] public KnnContainer m_container;
		[ReadOnly] public NativeSlice<float3> m_queryPositions;
		[ReadOnly] public NativeSlice<float> m_queryRadii;


		public NativeArray<RangeQueryResult> Results;

		public QueryMultiRangesBatchJob(KnnContainer container, NativeArray<float3> queryPositions, NativeArray<float> ranges, NativeArray<RangeQueryResult> results) {
			m_container = container;
			m_queryPositions = queryPositions;
			m_queryRadii = ranges;
			
			Results = results;
		}

		public void Execute(int index) {
			// Write results to proper slice!
		
			var tempList = new NativeList<int>(Allocator.Temp);
			m_container.QueryRange(m_queryPositions[index], m_queryRadii[index], tempList);

			var result = Results[index];
			result.SetResults(tempList);

			Results[index] = result;
			
		}
	}
	
	[BurstCompile(CompileSynchronously = true)]
	public struct QueryRangesBatchJob : IJobParallelFor {
		[ReadOnly] public KnnContainer m_container;
		[ReadOnly] public NativeSlice<float3> m_queryPositions;
		[ReadOnly] public float m_SearchRadius;

		public NativeArray<RangeQueryResult> Results;

		public QueryRangesBatchJob(KnnContainer container, NativeArray<float3> queryPositions, float searchSearchRadius, NativeArray<RangeQueryResult> results) {
			m_container = container;
			m_queryPositions = queryPositions;
			m_SearchRadius = searchSearchRadius;
			
			Results = results;
		}

		public void Execute(int index) {
			// Write results to proper slice!
		
			var tempList = new NativeList<int>(Allocator.Temp);
			m_container.QueryRange(m_queryPositions[index], m_SearchRadius, tempList);

			var result = Results[index];
			result.SetResults(tempList);

			Results[index] = result;
			
		}
	}
	
	[BurstCompile(CompileSynchronously = true)]
	public struct KnnRebuildJob : IJob {
		KnnContainer m_container;

		public KnnRebuildJob(KnnContainer container) {
			m_container = container;
		}

		void IJob.Execute() {
			m_container.Rebuild();
		}
	}
}