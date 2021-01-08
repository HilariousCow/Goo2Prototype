using System;
using KNN.Jobs;
using Ludopathic.Goo.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Jobs;

using UnityEngine.ParticleSystemJobs;
using UnityEngine;
public static class JobNativeMultiHashMapUniqueHashExtensions
    {
        [JobProducerType(typeof(JobNativeMultiHashMapUniqueHashExtensions.JobNativeMultiHashMapMergedSharedKeyIndicesProducer<>))]
        public interface IJobNativeMultiHashMapMergedSharedKeyIndices
        {
            // The first time each key (=hash) is encountered, ExecuteFirst() is invoked with corresponding value (=index).
            void ExecuteFirst(int index);
 
            // For each subsequent instance of the same key in the bucket, ExecuteNext() is invoked with the corresponding
            // value (=index) for that key, as well as the value passed to ExecuteFirst() the first time this key
            // was encountered (=firstIndex).
            void ExecuteNext(int firstIndex, int index);
        }
        
        internal struct JobNativeMultiHashMapMergedSharedKeyIndicesProducer<TJob>
            where TJob : struct, IJobNativeMultiHashMapMergedSharedKeyIndices
        {
            [ReadOnly] public NativeMultiHashMap<int, int> HashMap;
            internal TJob JobData;
 
            private static IntPtr s_JobReflectionData;
 
            internal static IntPtr Initialize()
            {
                if (s_JobReflectionData == IntPtr.Zero)
                {
                    s_JobReflectionData = JobsUtility.CreateJobReflectionData(typeof(JobNativeMultiHashMapMergedSharedKeyIndicesProducer<TJob>), typeof(TJob), (ExecuteJobFunction)Execute);
                }
 
                return s_JobReflectionData;
            }
 
            delegate void ExecuteJobFunction(ref JobNativeMultiHashMapMergedSharedKeyIndicesProducer<TJob> jobProducer, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);
 
            public static unsafe void Execute(ref JobNativeMultiHashMapMergedSharedKeyIndicesProducer<TJob> jobProducer, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                while (true)
                {
                    int begin;
                    int end;
 
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                    {
                        return;
                    }
 
                    var bucketData = jobProducer.HashMap.GetUnsafeBucketData();
                    var buckets = (int*)bucketData.buckets;
                    var nextPtrs = (int*)bucketData.next;
                    var keys = bucketData.keys;
                    var values = bucketData.values;
 
                    for (int i = begin; i < end; i++)
                    {
                        int entryIndex = buckets[i];
 
                        while (entryIndex != -1)
                        {
                            var key = UnsafeUtility.ReadArrayElement<int>(keys, entryIndex);
                            var value = UnsafeUtility.ReadArrayElement<int>(values, entryIndex);
                            int firstValue;
 
                            NativeMultiHashMapIterator<int> it;
                            jobProducer.HashMap.TryGetFirstValue(key, out firstValue, out it);
 
                            // [macton] Didn't expect a usecase for this with multiple same values
                            // (since it's intended use was for unique indices.)
                            // https://forum.unity.com/threads/ijobnativemultihashmapmergedsharedkeyindices-unexpected-behavior.569107/#post-3788170
                            if (entryIndex == it.GetEntryIndex())
                            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                                JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf(ref jobProducer), value, 1);
#endif
                                jobProducer.JobData.ExecuteFirst(value);
                            }
                            else
                            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                                var startIndex = math.min(firstValue, value);
                                var lastIndex = math.max(firstValue, value);
                                var rangeLength = (lastIndex - startIndex) + 1;
 
                                JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf(ref jobProducer), startIndex, rangeLength);
#endif
                                jobProducer.JobData.ExecuteNext(firstValue, value);
                            }
 
                            entryIndex = nextPtrs[entryIndex];
                        }
                    }
                }
            }
        }
 
        public static unsafe JobHandle Schedule<TJob>(this TJob jobData, NativeMultiHashMap<int, int> hashMap, int minIndicesPerJobCount, JobHandle dependsOn = new JobHandle())
            where TJob : struct, IJobNativeMultiHashMapMergedSharedKeyIndices
        {
            var jobProducer = new JobNativeMultiHashMapMergedSharedKeyIndicesProducer<TJob>
            {
                HashMap = hashMap,
                JobData = jobData
            };
 
            var scheduleParams = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref jobProducer)
                , JobNativeMultiHashMapMergedSharedKeyIndicesProducer<TJob>.Initialize()
                , dependsOn
                , ScheduleMode.Parallel
            );
 
            return JobsUtility.ScheduleParallelFor(ref scheduleParams, hashMap.GetUnsafeBucketData().bucketCapacityMask + 1, minIndicesPerJobCount);
        }
    }

namespace Ludopathic.Goo.Jobs
{
   
    [BurstCompile]
    public struct JobSetAcceleration : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float2> ValueToSet;
        
        [WriteOnly]
        public NativeArray<float2> AccumulatedAcceleration;

        
        public void Execute(int index)
        {
            AccumulatedAcceleration[index] = ValueToSet[index];
        }
    }
    
    

    [BurstCompile]
    public struct JobCursorsInfluenceBlobs : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float2> CursorPositions;
        [ReadOnly]
        public NativeArray<float2> CursorVelocities;
        [ReadOnly]
        public NativeArray<float> CursorRadius;
        
        
        [ReadOnly]
        public NativeArray<float2> BlobPositions;
      
        public NativeArray<float2> BlobAccelAccumulator;
        
        //For each cursor.
        //May be more efficient to go through each blob.
        //Also just the blobs close enough for blob influence
        public void Execute(int index)
        {
            float2 blobPos = BlobPositions[index];
            float2 blobAccel = BlobAccelAccumulator[index];
            
            //note this is n vs n.
            for (int jIndex = 0; jIndex < CursorPositions.Length; jIndex++)
            {
                float2 curPos = CursorPositions[jIndex];
                float2 curVel = CursorVelocities[jIndex];
                float cursorRadSq = CursorRadius[jIndex] * CursorRadius[jIndex];
                
                //Todo: Would it be good to make a job to store the sqr distance of each cursor to each blob?
                float deltaSq = math.distancesq(curPos, blobPos);

                if (deltaSq <= cursorRadSq)
                {
                    float delta = math.sqrt(deltaSq);
                    float invDelta = 1.0f - math.clamp(delta / CursorRadius[jIndex], 0f, 1f);//closer means more force transferred.
                    blobAccel += invDelta * curVel;//todo add some kinda proportion control thing.
                    //todo: id checks? or really, we should block the blob values into groups and stuff first.
                }
            }

            BlobAccelAccumulator[index] = blobAccel;
        }
    }
    
    [BurstCompile]
    public struct JobVelocityInfluenceFalloff : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float2> BlobPositions;
        [ReadOnly]
        public NativeArray<float2> BlobVelocities;
        
        [ReadOnly]
        public NativeArray<RangeQueryResult> BlobNearestNeighbours;//The list we are iterating through in execute

        [ReadOnly]
        public float InfluenceModulator;//todo: make array
        
        [ReadOnly]
        public NativeArray<float> InfluenceRadius;//todo: point at blob radii array
   
        
        public NativeArray<float2> BlobAccelAccumulator;
        
        //For each blob
        //Add force based on a nearby blob's velocity.
        //Also just the blobs close enough for blob influence
        public void Execute(int index)
        {
            float2 blobPos = BlobPositions[index];
            float2 blobVel = BlobPositions[index];
            
            RangeQueryResult oneBlobsNearestNeighbours = BlobNearestNeighbours[index];

            int numNearestNeighboursToCompare = oneBlobsNearestNeighbours.Length;//may not need this. not sure.
            
            float2 accumulateAccel = float2.zero;
            
            //note this is n vs n.
            for (int jIndex = 0; jIndex < numNearestNeighboursToCompare; jIndex++)
            {
                int indexOfOtherBlob = oneBlobsNearestNeighbours[jIndex];
                
                if(index == indexOfOtherBlob) continue;//don't self influence
                
                float2 otherPos = BlobPositions[indexOfOtherBlob];
                float2 otherVel = BlobVelocities[indexOfOtherBlob];
                float otherRadius = InfluenceRadius[indexOfOtherBlob];
                //only care about the difference between our velocities, not the other person's absolute
                float2 velocityDelta =  otherVel - blobVel;
         
                float dist = math.distance(blobPos, otherPos);//todo: see if we can use sq distance for falloff. I doubt it but maybe?
                float distFrac =math.clamp(  dist / otherRadius, 0.0f, 1.0f);
               
                float invDelta = 1.0f - distFrac;//closer means more force transferred.
      
                accumulateAccel +=/* dot */ InfluenceModulator * invDelta * velocityDelta;//todo add some kinda proportion control thing.
             
            }

            BlobAccelAccumulator[index] += accumulateAccel;
        }
    }
    
    [BurstCompile]
    //This is actually "apply differential" so it's used for acceleration->velocity, and velocity->position
    public struct JobApplyDerivative : IJobParallelFor
    {
        [ReadOnly]
        public float DeltaTime;
        
        [ReadOnly]
        public NativeArray<float2> AccumulatedAcceleration;

        public NativeArray<float2> VelocityInAndOut;
        
        public void Execute(int index)
        {
            float2 vel = VelocityInAndOut[index];

            vel = vel + AccumulatedAcceleration[index] * DeltaTime;

            VelocityInAndOut[index] = vel;
        }
    }
    
    [BurstCompile]
    //Combine the above into a two-fer to reduce overhead
    public struct JobApplyAcceelrationAndVelocity : IJobParallelFor
    {
        [ReadOnly]
        public float DeltaTime;
        
        [ReadOnly]
        public NativeArray<float2> AccumulatedAcceleration;

        public NativeArray<float2> VelocityInAndOut;
        
        public NativeArray<float2> PositionInAndOut;
        
        public void Execute(int index)
        {
            float2 vel = VelocityInAndOut[index];
            float2 pos = PositionInAndOut[index];

            vel = vel + AccumulatedAcceleration[index] * DeltaTime;
            pos = pos + VelocityInAndOut[index] * DeltaTime;
            
            VelocityInAndOut[index] = vel;
            PositionInAndOut[index] = pos;
        }
    }


    public struct SpringEdge
    {
        public int A, B;

        public SpringEdge(int a, int b)
        {
            A = math.min(a, b);
            B = math.max(a, b);
        }

        public long CustomHashCode()
        {
            long a = A;
            long b = B;
            return (a + b) * (a + b + 1) / 2 + a;
        }
    }
    [BurstCompile]
    public struct JobCompileUniqueEdges : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<RangeQueryResult> BlobNearestNeighbours;//The list we are iterating through in execute

     
        public NativeHashSet<long>.ParallelWriter UniqueEdges;

        
        [WriteOnly]
        public NativeMultiHashMap<int, int>.ParallelWriter Edges;
        
        
        public void Execute(int index)
        {
            RangeQueryResult blobNearestNeighbour = BlobNearestNeighbours[index];
            for (int j = 0; j < blobNearestNeighbour.Length; j++)
            {
                int indexOfOther = blobNearestNeighbour[j];
                if( index == indexOfOther) continue;//ignore self finds.
                
                SpringEdge edge = new SpringEdge(index, indexOfOther);

                long hash = edge.CustomHashCode();
                if (UniqueEdges.Add(hash))//only allow unique EDGES.
                {
                  //  Debug.Log($"Adding unique edge: {edge.A}, { edge.B} " );
                    Edges.Add(edge.A, edge.B);
                    
                 //  Debug.Log($"Adding unique edge: {edge.B}, { edge.A} " );
                    Edges.Add(edge.B, edge.A);
                }
                else
                {
                  //  Debug.Log($"Hash Clash: {edge.A}, { edge.B} " );
                }
            }
        }
    }

   
 
    [BurstCompile]
    public struct JobUniqueSpringForce : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float2> Positions;
        
        [ReadOnly]
        public NativeArray<float2> Velocity;//used to figure out counter spring force.
        
        //read and write
        //what was the "allow writing outside of current index thingy?
        public NativeArray<float2> AccelerationAccumulator;//ONLY affect my own acceleration so that there's no clashing.

        [ReadOnly]
        public NativeMultiHashMap<int, int> Springs;
        
        //note might be allowed to combined these into goo physics settings.
        [ReadOnly]
        public float MaxEdgeDistanceRaw;
        
        [ReadOnly]
        public float SpringConstant;
        
        [ReadOnly]
        public float DampeningConstant;
    
        public void Execute(int index)
        {
            float2 thisBlobsPosition = Positions[index];
            float2 thisBlobVelocity = Velocity[index];
            for (bool Success = Springs.TryGetFirstValue(index, out int otherIndex, out NativeMultiHashMapIterator<int> It); Success; )
            {
              

                //Debug.Log($"Spring first index: {index}, other index {otherIndex}" );
            
                float2 otherBlobPos = Positions[otherIndex];
                float2 otherBlobVel = Velocity[otherIndex];

            
            
                //Debug only
            /*    {
                    float3 posAViz = thisBlobsPosition.xxy;
                    posAViz.y = 0;
                    float3 posBViz = otherBlobPos.xxy;
                    posBViz.y = 0;
                    Debug.DrawLine(posAViz, math.lerp(posAViz, posBViz, 0.45f), Color.yellow);
                     Debug.DrawLine(posBViz, math.lerp(posAViz, posBViz, 0.55f), Color.blue);
                }*/

                float2 delta = otherBlobPos - thisBlobsPosition;
                float2 velocityDelta = otherBlobVel - thisBlobVelocity;
            
                float2 dir = math.normalize(delta);

                float deltaDist = math.length(delta); //pos b is the origin of the spring

                float speedAlongSpring = math.dot(dir, velocityDelta);
                float frac = deltaDist / MaxEdgeDistanceRaw;

                float targetFrac = 0.5f;
                float distanceFromTarget = (frac - targetFrac) * 2.0f; //just position based.

                float constantForce = distanceFromTarget * SpringConstant;
                float dampening = speedAlongSpring * DampeningConstant;

                float2 forceAlongSpring = (dampening + constantForce) * dir;
            
                //  Debug.Log($"Acceleration Accumulator[{firstIndex}] before: {AccelerationAccumulator[firstIndex] }" );
                AccelerationAccumulator[index] += forceAlongSpring;
            
                //   AccelerationAccumulator[index] -= forceAlongSpring;
                //  Debug.Log($"Acceleration Accumulator[{firstIndex}] after: {AccelerationAccumulator[firstIndex] }" );

                Success = Springs.TryGetNextValue(out otherIndex, ref It);
            }
        }

        void AccumulateSpringForce(int index, int otherIndex)
        {
            float2 thisBlobsPosition = Positions[index];
            float2 thisBlobVelocity = Velocity[index];

            //Debug.Log($"Spring first index: {index}, other index {otherIndex}" );
            
            float2 otherBlobPos = Positions[otherIndex];
            float2 otherBlobVel = Velocity[otherIndex];

            
            
            //Debug only
            {
                float3 posAViz = thisBlobsPosition.xxy;
                posAViz.y = 0;
                float3 posBViz = otherBlobPos.xxy;
                posBViz.y = 0;
                //Debug.DrawLine(posAViz, math.lerp(posAViz, posBViz, 0.45f), Color.yellow);
               // Debug.DrawLine(posBViz, math.lerp(posAViz, posBViz, 0.55f), Color.blue);
            }

            float2 delta = otherBlobPos - thisBlobsPosition;
            float2 velocityDelta = otherBlobVel - thisBlobVelocity;
            
            float2 dir = math.normalize(delta);

            float deltaDist = math.length(delta); //pos b is the origin of the spring

            float speedAlongSpring = math.dot(dir, velocityDelta);
            float frac = deltaDist / MaxEdgeDistanceRaw;

            float targetFrac = 0.5f;
            float distanceFromTarget = (frac - targetFrac) * 2.0f; //just position based.

            float constantForce = distanceFromTarget * SpringConstant;
            float dampening = speedAlongSpring * DampeningConstant;

            float2 forceAlongSpring = (dampening + constantForce) * dir;
            
            //  Debug.Log($"Acceleration Accumulator[{firstIndex}] before: {AccelerationAccumulator[firstIndex] }" );
            AccelerationAccumulator[index] += forceAlongSpring;
            
            //   AccelerationAccumulator[index] -= forceAlongSpring;
            //  Debug.Log($"Acceleration Accumulator[{firstIndex}] after: {AccelerationAccumulator[firstIndex] }" );
        }
    }

    [BurstCompile]
    public struct JobSpringForceUsingKNNResults : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<RangeQueryResult> BlobNearestNeighbours;//The list we are iterating through in execute
        
        [ReadOnly]
        public NativeArray<float2> Positions;
        
        [ReadOnly]
        public NativeArray<float2> Velocity;//used to figure out counter spring force.
        
        //read and write
        public NativeArray<float2> AccelerationAccumulator;//ONLY affect my own acceleration so that there's no clashing.

      
        
        [ReadOnly]
        public float MaxEdgeDistanceRaw;
        
        [ReadOnly]
        public float SpringConstant;
        
        [ReadOnly]
        public float DampeningConstant;
        
        //for each blob
        public void Execute(int index)
        {
            float2 thisBlobsPosition = Positions[index];
            
            RangeQueryResult oneBlobsNearestNeighbours = BlobNearestNeighbours[index];
            int numBlobEdges = oneBlobsNearestNeighbours.Length;

            if (numBlobEdges == 1)
            {
                return;
            }
            
            
            int numBlobsToSample = numBlobEdges;
            
            
         //   Debug.Log($"index a:{index}, has num neighbours:{numBlobEdges}. Num to sample: {numBlobsToSample}");
            
            
            //float MaxEdgeDistanceSq = MaxEdgeDistanceRaw * MaxEdgeDistanceRaw;
            //for each nearby blob

            float2 thisBlobVelocity = Velocity[index];
            float2 accumulateAcceleration = float2.zero;
            
            
            //Vector3 pos = new Vector3(thisBlobsPosition.x, 0.0f, thisBlobsPosition.y);//debug only
            for (int j = 0; j < numBlobsToSample; j++)
            {
                int indexOfOtherBlob = oneBlobsNearestNeighbours[j];
                
                if(indexOfOtherBlob == index) continue;
                
                float2 otherBlobPos = Positions[indexOfOtherBlob];
               // float2 halfWay = math.lerp(thisBlobsPosition, otherBlobPos, 0.5f);
            //    Vector3 halfWayPoint = new Vector3(halfWay.x, 0.0f, halfWay.y);//debug only
                
                float2 delta = otherBlobPos - thisBlobsPosition;
                float2 dir = math.normalize(delta);
                //float deltaDistSq = math.lengthsq(delta);
                //maybe skip out if delta dist is small? Ideally something deals with it. Perhaps a pass where we de-penetrate all blobs until there are no more blobs overlapping, using a stack of paired blobs.
               
                float2 otherBlobVel = Velocity[indexOfOtherBlob];
                float2 velocityDelta = otherBlobVel - thisBlobVelocity;
                
               
                float deltaDist = math.length(delta);//pos b is the origin of the spring
             
                float speedAlongSpring = math.dot(dir, velocityDelta);
                
               // float2 crossDir =  new float2(dir.y, -dir.x);//to stop twisting
             //   float speedAcrossSpring = math.dot(crossDir, velocityDelta);
                    
                float frac = deltaDist / MaxEdgeDistanceRaw;

                float targetFrac = 0.8f;
                float distanceFromTarget = (frac-targetFrac) * 2.0f;//just position based.

                //float invFrac = math.clamp( 1.0f - frac, 0.0f, 1.0f);
               // float falloff = invFrac * invFrac;
             //   falloff = 1.0f;
                float constantForce = distanceFromTarget * SpringConstant;
                float dampening =  speedAlongSpring * DampeningConstant;


                float2 forceAlongSpring = (dampening + constantForce) * dir;// * falloff;  
              //float2 forceAcrossSpring = 

                accumulateAcceleration += forceAlongSpring ;
                
          //      Debug.Log($"index a:{index}, index b:{indexOfOtherBlob}");
         //       Debug.DrawLine(pos, halfWayPoint, Color.Lerp(Color.green, Color.red, frac));
            }

            
         //   Vector3 accelOffset = new Vector3(accumulateAcceleration.x, 0.0f, accumulateAcceleration.y);
        //    Debug.DrawLine(pos, pos + accelOffset);
            AccelerationAccumulator[index] += accumulateAcceleration;
        }
    }
    
    [BurstCompile]
    public struct JobFilterByTeam : IJobParallelForFilter
    {
        [ReadOnly]
        public NativeArray<int> TeamIDs;
        
        [ReadOnly]
        public int TeamID;

        public bool Execute(int index)
        {
            return TeamID == TeamIDs[index];
        }
    }
    
    [BurstCompile]
    public struct JobFloodFillIDsKnn : IJob
    {
   
        [ReadOnly]
        public NativeArray<RangeQueryResult> BlobNearestNeighbours;

        //read and write
        public NativeArray<int> GroupIDs;
        public NativeQueue<int> FloodQueue;

        [WriteOnly]
        public NativeArray<int> NumGroups;
        
        public void Execute()
        {
            int groupID = 0;
            
            for (int i = 0; i < BlobNearestNeighbours.Length; i++)
            {

                int blobIndex =i;
                if (GroupIDs[blobIndex] < 0)
                {
                    Fill(blobIndex, groupID, ref FloodQueue);
                    while (!FloodQueue.IsEmpty())
                    {
                        int neighbourIndex = FloodQueue.Dequeue();
                        Fill(neighbourIndex, groupID, ref FloodQueue);
                    }
                }
                groupID++;
            }

            //simple number of groups output
            NumGroups[0] = groupID;
        }

        //Hmm. starting to be a mess because we don't populate KD trees per team yet (we just "borrow" z to be team and separate. Kinda hacky)
        private void Fill(int index, int id, ref NativeQueue<int> queue)
        {
            if (GroupIDs[index] < 0)//only flood fill unassigned blobs
            {
                GroupIDs[index] = id;
                RangeQueryResult neighbours = BlobNearestNeighbours[index];
                int neighbourMax =neighbours.Length;
                for (int j = 0; j < neighbourMax; j++)
                {
                    int indexOfNearestNeighbour = neighbours[j];
                    
                    if (indexOfNearestNeighbour == index) continue;
                    
                    queue.Enqueue(neighbours[j]);
                    
                }
            }
        }
    }
    
     
    [BurstCompile]
    public struct JobFloodFillIDsUniqueEdges : IJob
    {
   
        [ReadOnly]
        public NativeMultiHashMap<int, int> Springs;

        //read and write
        public NativeArray<int> GroupIDs;
        public NativeQueue<int> FloodQueue;

        [WriteOnly]
        public NativeArray<int> NumGroups;
        
        public void Execute()
        {
            int groupID = 0;


           
            for (int i = 0; i < GroupIDs.Length; i++)
            {
                int blobIndex =i;
                if (GroupIDs[blobIndex] < 0)
                {
                    Fill(blobIndex, groupID, ref FloodQueue);
                    while (!FloodQueue.IsEmpty())
                    {
                        int neighbourIndex = FloodQueue.Dequeue();
                        Fill(neighbourIndex, groupID, ref FloodQueue);
                    }
                    groupID++;
                }
            }

            //simple number of groups output
            NumGroups[0] = groupID;
        }

        //Hmm. starting to be a mess because we don't populate KD trees per team yet (we just "borrow" z to be team and separate. Kinda hacky)
        private void Fill(int index, int id, ref NativeQueue<int> queue)
        {
            if (GroupIDs[index] < 0)//only flood fill unassigned blobs
            {
                GroupIDs[index] = id;
                
                for (bool Success = Springs.TryGetFirstValue(index, out int Value, out NativeMultiHashMapIterator<int> It); Success; )
                {
                    int indexOfNearestNeighbour = Value;
                    
                    if (indexOfNearestNeighbour == index) continue;//can remove i think
                    
                    queue.Enqueue(indexOfNearestNeighbour);
                    Success = Springs.TryGetNextValue(out Value, ref It);
                }
            }
        }
    }
    
    //Accumulate friction
    [BurstCompile]
    public struct JobApplyLinearAndConstantFriction : IJobParallelFor
    {
        [ReadOnly]
        public float DeltaTime;
        
        
        //todo: "jerk" friction - t*t
        [ReadOnly]
        public float LinearFriction;//might be made per blob.
        
        [ReadOnly]
        public float ConstantFriction;//might be made per blob.

        [ReadOnly]
        public NativeArray<float2> Velocity;
        
       
        public NativeArray<float2> AccumulatedAcceleration;

        
        public void Execute(int index)
        {
            var accel = AccumulatedAcceleration[index];
            var vel = Velocity[index];
            float speedSquared = math.lengthsq(vel);
            
            if (speedSquared > 0.0f)
            {
                float linearFriction =  speedSquared * LinearFriction;
                float2 velocityDirection = math.normalize(vel);
                float rawSpeed = math.sqrt(speedSquared);
                
               
                //Linear friction should only ever bring us down to zero.
                float speedChangeMax = rawSpeed;
                float speedChangeDueToConstant = ConstantFriction * DeltaTime;
                float clampedSpeedChange = math.min(speedChangeMax, speedChangeDueToConstant) / DeltaTime;
                
                float2 constantFrictionForce = (velocityDirection * clampedSpeedChange ) ;
                float2 linearFrictionForce = (vel * DeltaTime * linearFriction);

                float2 frictionForce = constantFrictionForce + linearFrictionForce;
                
                accel -= frictionForce;
                
                //I bet i'm meant to have a log in here somwhere.
                
            }
            
            AccumulatedAcceleration[index] = accel;
        }
    }
    
    //Hopefully temp because we might be able to get away with a 2d search at some point
    [BurstCompile]
    public struct JobCopyBlobInfoToFloat3 : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<int> BlobTeams;
        public NativeArray<float2> BlobPos;
        
        
        [WriteOnly]
        public NativeArray<float3> BlobPosFloat3;
        public void Execute(int index)
        {
            BlobPosFloat3[index] = new float3(BlobTeams[index] * 1000.0f, //Hack: make the team spread REALLY FAR so that there's almost no chance of them coming back as being within range, since the KNN Queries use sqDistance checks
                BlobPos[index].x,BlobPos[index].y) ;
        }
    }

    //note: would be cool for this to be done outside the sim update, and for it to take time-since-last-game-frame into account
    [BurstCompile]
    public struct JobCopyBlobsToTransforms : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<float2> BlobPos;
        public void Execute(int index, TransformAccess transform)
        {
            transform.position =  new float3(BlobPos[index].x,0f,BlobPos[index].y) ;
        }
    }
    
    [BurstCompile]
    public struct JopCopyBlobsToParticleSystem : IJobParticleSystemParallelForBatch
    {
        [ReadOnly]
        public NativeArray<float2> positions;
        [ReadOnly]
        public NativeArray<float2> velocities;
        [ReadOnly]
        public NativeArray<Color> colors;
 
        public void Execute(ParticleSystemJobData jobData, int startIndex, int count)
        {
            var startColors = jobData.startColors;
            var particleVels = jobData.velocities;
            var particlePos = jobData.positions;
            
            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                startColors[i] = colors[i];
                particleVels[i] = new Vector3(velocities[i].x, 0.0f, velocities[i].y);
                particlePos[i] = new Vector3(positions[i].x, 0.0f, positions[i].y);
            }
        }
    }
    
    [BurstCompile]
    public struct JobDebugColorisationInt : IJobParallelFor
    {
        [ReadOnly]
        public int minVal ;
        
        [ReadOnly]
        public int maxVal ;
        
        [ReadOnly]
        public NativeArray<int> values;
        
        [WriteOnly]
        public NativeArray<Color> colors;
 
        public void Execute(int index)
        {
            float fraction = (values[index] - minVal) / (float)(maxVal - minVal);
            colors[index] = Color.HSVToRGB(fraction*0.75f, 1f,1f);
        }
    }
    
    
    [BurstCompile]
    public struct JobDebugColorisationKNNRangeQuery : IJobParallelFor
    {
        [ReadOnly]
        public int minVal ;
        
        [ReadOnly]
        public int maxVal ;
        
        [ReadOnly]
        public NativeArray<RangeQueryResult> values;
        
        [WriteOnly]
        public NativeArray<Color> colors;
 
        public void Execute(int index)
        {
            float fraction = (values[index].Length - minVal) / (float)(maxVal - minVal);
            colors[index] = Color.HSVToRGB(fraction*0.75f, 1f,1f);
        }
    }
    
    
    [BurstCompile]
    public struct JobDebugColorisationFloat : IJobParallelFor
    {
        [ReadOnly]
        public float minVal ;
        
        [ReadOnly]
        public float maxVal ;
        
        [ReadOnly]
        public NativeArray<float> values;
        
        [WriteOnly]
        public NativeArray<Color> colors;
 
        public void Execute(int index)
        {
            float fraction = (values[index] - minVal) / (maxVal - minVal);
            colors[index] = Color.HSVToRGB(fraction*0.75f, 1f,1f);
        }
    }
    
    
    [BurstCompile]
    public struct JobDebugColorisationFloat2Magnitude : IJobParallelFor
    {
        [ReadOnly]
        public float minVal ;
        
        [ReadOnly]
        public float maxVal ;
        
        [ReadOnly]
        public NativeArray<float2> values;
        
        [WriteOnly]
        public NativeArray<Color> colors;
 
        public void Execute(int index)
        {
            float length = math.length(values[index]);
            float fraction = (length - minVal) / (maxVal - minVal);
            colors[index] = Color.HSVToRGB(fraction*0.75f, 1f,1f);
        }
    }
    
    //TODO: vis that shows 2ds as separate axes.
    [BurstCompile]
    public struct JobDebugColorisationFloat2XY : IJobParallelFor
    {
     
        [ReadOnly]
        public float maxVal ;
        
        [ReadOnly]
        public NativeArray<float2> values;
        
        [WriteOnly]
        public NativeArray<Color> colors;
 
        public void Execute(int index)
        {
           
            float xFraction = ((values[index].x - maxVal) / (maxVal + maxVal) + 1f) * 0.5f;
            float yFraction = ((values[index].y - maxVal) / (maxVal + maxVal) + 1f) * 0.5f;
            
         
            Color col = new Color(xFraction, yFraction, 1.0f, 1.0f);
            colors[index] = col;
        }
    }
    
    //TODO: vis that shows 2ds as separate axes.
    [BurstCompile]
    public struct JobCalculateAABB : IJob
    {
        [ReadOnly]
        public NativeArray<float2> Positions;

        
        public NativeArray<Bounds> Bounds;//singleton just for reading
        public void Execute()
        {
            float2 min = new float2(float.MaxValue, float.MaxValue);
            float2 max = new float2(float.MinValue, float.MinValue);
            for (int i = 0; i < Positions.Length; i++)
            {
                float2 pos = Positions[i];

                max = math.max(max, pos);
                min = math.min(min, pos);
            }

            Bounds bounds = new Bounds();
            bounds.min = new Vector3(min.x, 0.0f, min.y);
            bounds.max = new Vector3(max.x, 0.0f, max.y);
            Bounds[0] = bounds;
            
        }
    }
    
    
}