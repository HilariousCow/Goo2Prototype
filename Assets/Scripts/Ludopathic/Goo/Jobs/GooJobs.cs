using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace Ludopathic.Goo.Jobs
{
    [BurstCompile]
    public struct JobResetAcceleration : IJobParallelFor
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
    public struct JobCursorsInfluenceBlobs : IJobParallelFor
    {
        //not sure if i need delta time?
        
        [ReadOnly]
        public NativeArray<float2> CursorPositions;
        
        [ReadOnly]
        public NativeArray<float2> CursorVelocities;

        [ReadOnly]
        public NativeArray<float> CursorRadius;
        
        [ReadOnly]
        public NativeArray<float2> BlobPositions;
      
        public NativeArray<float2> BlobAccelAccumulator;
        
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
                    blobAccel += invDelta * curVel;
                }
            }

            BlobAccelAccumulator[index] = blobAccel;
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

    public struct BlobEdge
    {
        public int BlobIndexA;
        public int BlobIndexB;
    }
    
    [BurstCompile]
    //this could do with more nuance/smaller search space but it basically works.
    public struct JobFindEdges : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float2> Positions;
        
   
        
        [ReadOnly]
        public float MaxEdgeDistanceSq;

        [ReadOnly]
        public int MaxEdgesPerBlob;
        
        
        [WriteOnly]
        public NativeArray<int> BlobEdgeCount;
        [WriteOnly]
        [NativeDisableParallelForRestriction] 
        public NativeArray<BlobEdge> BlobEdges;//Position.length * MaxEdgesPerBlob
        
    
        
        public void Execute(int index)//for a blob
        {
            int numBlobsFound = 0;
            float2 posA = Positions[index];
            for (int j = 0; j < Positions.Length && numBlobsFound < MaxEdgesPerBlob; j++)
            {
                if (j == index) continue;
                
                float2 posB = Positions[j];
                float sqDist = math.lengthsq(posA - posB);

                int indexOfEdgeListEntry = index * MaxEdgesPerBlob;//staggered index.
                
                if (sqDist < MaxEdgeDistanceSq)
                {
                    int newEdgeEntry = indexOfEdgeListEntry + numBlobsFound;//out of index range??
                    BlobEdges[newEdgeEntry] = new BlobEdge
                    {
                        BlobIndexA = index, 
                        BlobIndexB = j
                    };
                    numBlobsFound++;

                    BlobEdgeCount[index] = numBlobsFound;

                    if (numBlobsFound == MaxEdgesPerBlob) break;
                }
            }
        }
    }

    [BurstCompile]
    public struct JobSpringForce : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<float2> Positions;
        
        //read and write
        public NativeArray<float2> AccelerationAccumulator;//ONLY affect my own acceleration so that there's no clashing.

        [ReadOnly]
        public int MaxEdgesPerBlob;
        
        [ReadOnly]
        public NativeArray<int> BlobEdgeCount;
        
        [ReadOnly]
        [NativeDisableParallelForRestriction] 
        public NativeArray<BlobEdge> BlobEdges;

        [ReadOnly]
        public float MaxEdgeDistanceRaw;
        
        [ReadOnly]
        public float SpringConstant;
        
        //for each blob
        public void Execute(int index)
        {
            float2 posA = Positions[index];
            
            
            int numBlobEdges = BlobEdgeCount[index];
            //for each nearby blob
            for (int j = 0; j < numBlobEdges; j++)
            {
                int indexOfOtherBlobInBigArray = MaxEdgesPerBlob * index + j;

                int indexOfOtherBlob = BlobEdges[indexOfOtherBlobInBigArray].BlobIndexB;
                float2 posB = Positions[indexOfOtherBlob];
                
                //simple spring force at first
                float2 delta = posA - posB;
                float deltaDist = math.length(posA - posB);

                if (deltaDist > 0.0)
                {
                    float frac = math.clamp( deltaDist / MaxEdgeDistanceRaw, 0f, 1f);

                    frac *= frac;//power falloff before calculating spring force. i.e moves the spring force target center close to the other blob.
                    float k = frac - 0.5f;
                    float f = k * SpringConstant;

                    float2 dir = math.normalize(delta);
                    
                    //float2 force = -f * dir * (1.0f -frac) * (1.0f -frac);//v basic with falloff 
                    float2 force = -f * dir * (1.0f -frac) * (1.0f -frac);
                    
                    AccelerationAccumulator[index] += force;
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
            float speed = math.lengthsq(vel);
            
            if (speed > 0.0f)
            {
                float linearFriction =  speed * LinearFriction;
                var velocityDirection = math.normalize(vel);
                float2 frictionForce = (velocityDirection * ConstantFriction) + (vel * DeltaTime * linearFriction);
                accel -= frictionForce;
                //jerk accel += jerkFriction * DeltaTime * DeltaTime + velocityDirection;
            }
            
            AccumulatedAcceleration[index] = accel;
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
}