using KNN.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

using UnityEngine.ParticleSystemJobs;
using UnityEngine;

namespace Ludopathic.Goo.Jobs
{
    
    
    [BurstCompile]
    public struct JobZeroFloat2Array : IJobParallelFor
    {
     
        [WriteOnly]
        public NativeArray<float2> AccumulatedAcceleration;

        
        public void Execute(int index)
        {
            AccumulatedAcceleration[index] = float2.zero;
        }
    }

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
    public struct JobSetIntValue : IJobParallelFor
    {
        [WriteOnly]
        public NativeArray<int> ValuesToSet;

        [ReadOnly]
        public int Value;
        public void Execute(int index)
        {
            ValuesToSet[index] = Value;
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
        public int NumNearestNeighbours;
       
        public NativeArray<float2> BlobAccelAccumulator;

        [ReadOnly]
        public float InfluenceModulator;
        [ReadOnly]
        public float InfluenceRadius;
        [ReadOnly]
        public float InfluenceFalloff;
        
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
                
                //only care about the difference between our velocities, not the other person's absolute
                float2 velocityDelta =  otherVel - blobVel;
                
                //maybe only care about stuff ahead of you?

                //experiment
              //  float dot = math.dot(otherVel, math.normalizesafe(blobPos - otherPos) );
             //   dot = math.clamp(dot, 0.0f, 1.0f);
            
                float dist = math.distance(blobPos, otherPos);//todo: see if we can use sq distance for falloff. I doubt it but maybe?
                float distFrac =math.clamp(  dist / InfluenceRadius , 0.0f, 1.0f);
               
                float invDelta = 1.0f - distFrac;//closer means more force transferred.
              //  invDelta = math.pow(invDelta, InfluenceFalloff);
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
        public int NumNearestNeighbours;
        
        [ReadOnly]
        public float MaxEdgeDistanceRaw;
        
        [ReadOnly]
        public float SpringConstant;
        
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
            
            
            int numBlobsToSample = math.min(numBlobEdges, NumNearestNeighbours);
            
            
         //   Debug.Log($"index a:{index}, has num neighbours:{numBlobEdges}. Num to sample: {numBlobsToSample}");
            
            
            //float MaxEdgeDistanceSq = MaxEdgeDistanceRaw * MaxEdgeDistanceRaw;
            //for each nearby blob

            float2 thisBlobVelocity = Velocity[index];
            float2 accumulateAcceleration = float2.zero;
            
            
            Vector3 pos = new Vector3(thisBlobsPosition.x, 0.0f, thisBlobsPosition.y);//debug only
            for (int j = 0; j < numBlobsToSample; j++)
            {
                int indexOfOtherBlob = oneBlobsNearestNeighbours[j];
                
                if(indexOfOtherBlob == index) continue;
                float2 otherBlobPos = Positions[indexOfOtherBlob];
                Vector3 otherPos = new Vector3(otherBlobPos.x, 0.0f, otherBlobPos.y);//debug only
                
                float2 delta = otherBlobPos - thisBlobsPosition;
                //float deltaDistSq = math.lengthsq(delta);
                //maybe skip out if delta dist is small? Ideally something deals with it. Perhaps a pass where we de-penetrate all blobs until there are no more blobs overlapping, using a stack of paired blobs.
               
                float2 otherBlobVel = Velocity[indexOfOtherBlob];
                float2 velocityDelta = otherBlobVel - thisBlobVelocity;
                
               
                float deltaDist = math.length(delta);//pos b is the origin of the spring
                float2 dir = math.normalize(delta);

                float speedAlongSpring = math.dot(dir, velocityDelta);
                
               // float2 crossDir =  new float2(dir.y, -dir.x);//to stop twisting
             //   float speedAcrossSpring = math.dot(crossDir, velocityDelta);
                    
                float frac = deltaDist / MaxEdgeDistanceRaw;

                float targetFrac = 0.5f;
                float distanceFromTarget = (frac-targetFrac) * 2.0f;//just position based.

                float invFrac = math.clamp( 1.0f - frac, 0.0f, 1.0f);
                float falloff = invFrac * invFrac;
                falloff = 1.0f;
                float constantForce = distanceFromTarget * SpringConstant;
                float pullBackForce = distanceFromTarget * speedAlongSpring * 0.5f;
                

                float2 forceAlongSpring = (pullBackForce + constantForce) * dir * falloff;  
              //float2 forceAcrossSpring = 

                accumulateAcceleration += forceAlongSpring ;
                
          //      Debug.Log($"index a:{index}, index b:{indexOfOtherBlob}");
           //     Debug.DrawLine(pos, otherPos, Color.Lerp(Color.green, Color.red, frac));
            }

            
         //   Vector3 accelOffset = new Vector3(accumulateAcceleration.x, 0.0f, accumulateAcceleration.y);
        //    Debug.DrawLine(pos, pos + accelOffset);
            AccelerationAccumulator[index] += accumulateAcceleration;
        }
    }
    
    
    [BurstCompile]
    public struct JobFloodFillIDs : IJob
    {
        
        [ReadOnly]
        public NativeArray<RangeQueryResult> BlobNearestNeighbours;

        [ReadOnly]
        public int NumNearestNeighbours;
        
        //read and write
        public NativeArray<int> GroupIDs;
        public NativeQueue<int> FloodQueue;

        [WriteOnly]
        public NativeArray<int> NumGroups;
        
        public void Execute()
        {
            
            int id = 0;
            
            for (int i = 0; i < BlobNearestNeighbours.Length; i++)
            {
                if (GroupIDs[i] < 0)
                {
                    Fill(i, id, ref FloodQueue);
                    while (!FloodQueue.IsEmpty())
                    {
                        int neighbourIndex = FloodQueue.Dequeue();
                        Fill(neighbourIndex, id, ref FloodQueue);
                    }
                }
                id++;
            }

            //simple number of groups output
            NumGroups[0] = id;
        }

        private void Fill(int index, int id, ref NativeQueue<int> queue)
        {
            if (GroupIDs[index] < 0)//only flood fill unassigned blobs
            {
                GroupIDs[index] = id;
                RangeQueryResult neighbours = BlobNearestNeighbours[index];
                int neighbourMax = math.min(neighbours.Length, NumNearestNeighbours);
                for (int j = 0; j < neighbourMax; j++)
                {
                    int indexOfNearestNeighbour = neighbours[j];
                    
                    if (indexOfNearestNeighbour == index) continue;
                    
                    queue.Enqueue(neighbours[j]);
                    
                }
            }
        }

        /*
         Causes stack overflow on large scenes (though is technically correct)
        public void Execute()
        {
            int idToFillWith = 0;

            for (int i = 0; i < BlobNearestNeighbours.Length; i++)
            {
                if (GroupIDs[i] < 0)
                {
                    FillNeighboursWithID(i, idToFillWith);
                }
                idToFillWith++;
            }
        }

        private void FillNeighboursWithID(int index, int idToFillWith)
        {
            GroupIDs[index] = idToFillWith;
            RangeQueryResult nearestNeighbours = BlobNearestNeighbours[index];
            int numBlobsToCheck = math.min(NumNearestNeighbours, nearestNeighbours.Length);
            for (int j = 0; j < numBlobsToCheck; j++)//possibly start at 1 because 0 index is "us"
            {
                int indexOfNearestNeighbour = nearestNeighbours[j];
                if(index == indexOfNearestNeighbour) continue;//don't do infinite recursion!
                if (GroupIDs[indexOfNearestNeighbour] < 0)
                {
                    FillNeighboursWithID(indexOfNearestNeighbour, idToFillWith);
                }
            }
        }
        */
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
            BlobPosFloat3[index] = new float3(BlobTeams[index] * 100.0f, //Hack: make the team spread REALLY FAR so that there's almost no chance of them coming back as being within range, since the KNN Queries use sqDistance checks
                BlobPos[index].x,BlobPos[index].y) ;
        }
    }

    //Hopefully temp
    /*
    [BurstCompile]
    public struct JobCopyFloat3ToBlobs : IJobParallelFor
    {   [ReadOnly]
        public NativeArray<float3> BlobPosFloat3;
        [WriteOnly]
        public NativeArray<float2> BlobPos;
        public void Execute(int index)
        {
            BlobPos[index] = new float2(BlobPosFloat3[index].y,BlobPosFloat3[index].z) ;
        }
    }
    */
    
    
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
                if (pos.x > max.x) max.x = pos.x;
                if (pos.y > max.y) max.y = pos.y;
                if (pos.x < min.x) min.x = pos.x;
                if (pos.y < min.y) min.y = pos.y;
            }

            Bounds bounds = new Bounds();
            bounds.min = new Vector3(min.x, 0.0f, min.y);
            bounds.max = new Vector3(max.x, 0.0f, max.y);
            Bounds[0] = bounds;
            
        }
    }
    
    
}