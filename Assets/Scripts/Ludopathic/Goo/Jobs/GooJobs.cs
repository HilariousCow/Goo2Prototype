using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace Ludopathic.Goo.Jobs
{
    
    public struct JobResetAcceleration : IJobParallelFor
    {
     
        [WriteOnly]
        public NativeArray<float2> _accumulatedAcceleration;

        
        public void Execute(int index)
        {
            _accumulatedAcceleration[index] = float2.zero;
        }
    }

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
                float cursorRadSq = CursorRadius[jIndex];
                
                //Todo: Would it be good to make a job to store the sqr distance of each cursor to each blob?
                float deltaSq = math.distancesq(curPos, blobPos);

                float invDelta = math.clamp( (cursorRadSq - deltaSq) / cursorRadSq, 0f, 1f);
                blobAccel += invDelta * curVel;
            }

            BlobAccelAccumulator[index] = blobAccel;
        }
    }
    
    
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
    
    //Accumulate friction
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
            float2 accel = AccumulatedAcceleration[index];
            float2 vel = Velocity[index];
            float speed = math.length(vel);
            
            if (speed > 0.0f)
            {
                float linearFriction =  speed * LinearFriction;
                float2 velocityDirection = math.normalize(vel);
                accel += velocityDirection * ConstantFriction;
                accel += linearFriction * DeltaTime + velocityDirection;
                //jerk accel += jerkFriction * DeltaTime * DeltaTime + velocityDirection;
            }
            
            AccumulatedAcceleration[index] = accel;
        }
    }
    
    //note: would be cool for this to be done outside the sim update, and for it to take time-since-last-game-frame into account
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