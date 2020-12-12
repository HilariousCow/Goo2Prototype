using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ParticleSystemJobs;

public class SelfCollision : MonoBehaviour
{
    ParticleSystem m_ParticleSystem;
    CacheJob m_CacheJob;
    SortJob m_SortJob;
    CollisionJob m_CollisionJob;
    ApplyCollisionsJob m_ApplyCollisionsJob;

    NativeArray<SortKey> m_SortKeys;
    NativeQueue<Collision> m_Collisions;
 
    void Start()
    {
        m_ParticleSystem = GetComponent<ParticleSystem>();

        var main = m_ParticleSystem.main;
        var collision = m_ParticleSystem.collision;
        int maxParticleCount = main.maxParticles;

        m_SortKeys = new NativeArray<SortKey>(maxParticleCount, Allocator.Persistent);
        m_Collisions = new NativeQueue<Collision>(Allocator.Persistent);
        
        m_CacheJob = new CacheJob
        {
            sortKeys = m_SortKeys,
        };
        
        m_SortJob = new SortJob
        {
            sortKeys = m_SortKeys
        };

        m_CollisionJob = new CollisionJob
        {
            sortKeys = m_SortKeys,
            collisions = m_Collisions.AsParallelWriter(),
            bounce = collision.bounceMultiplier,
            radiusScale = collision.radiusScale,
            maxDiameter = main.startSize.constantMax * collision.radiusScale // TODO - handle different size curve modes and size over life if needed
        };

        m_ApplyCollisionsJob = new ApplyCollisionsJob
        {
            collisions = m_Collisions
        };
    }

    void OnDisable()
    {
       m_SortKeys.Dispose();
       m_Collisions.Dispose();
    }

    void OnParticleUpdateJobScheduled()
    {
        m_CollisionJob.bounce = m_ParticleSystem.collision.bounceMultiplier;
        
        var handle = m_CacheJob.ScheduleBatch(m_ParticleSystem, 2048);
        handle = m_SortJob.Schedule(m_ParticleSystem, handle);
        handle = m_CollisionJob.ScheduleBatch(m_ParticleSystem, 1024, handle);
        m_ApplyCollisionsJob.Schedule(m_ParticleSystem, handle);
    }

    struct SortKey : IComparable<SortKey>
    {
        public float Key;
        public int Index;

        public int CompareTo(SortKey other)
        {
            return Key.CompareTo(other.Key);
        }
    }

    struct Collision
    {
        public int Index;
        public float3 DeltaPosition;
        public float3 DeltaVelocity;
        
        public int Index2;
        public float3 DeltaPosition2;
        public float3 DeltaVelocity2;
    }
    
    [BurstCompile]
    struct CacheJob : IJobParticleSystemParallelForBatch
    {
        [WriteOnly]
        public NativeArray<SortKey> sortKeys;

        public void Execute(ParticleSystemJobData particles, int startIndex, int count)
        {
            var srcPositions = particles.positions;

            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
                sortKeys[i] = new SortKey { Key = srcPositions[i].x, Index = i };
        }
    }
    
    [BurstCompile]
    struct SortJob : IJobParticleSystem
    {
        public NativeArray<SortKey> sortKeys;
 
        public void Execute(ParticleSystemJobData particles)
        {
            new NativeSlice<SortKey>(sortKeys, 0, particles.count).Sort();
        }
    }
    
    [BurstCompile]
    struct CollisionJob : IJobParticleSystemParallelForBatch
    {
        [ReadOnly]
        public NativeArray<SortKey> sortKeys;

        [WriteOnly]
        public NativeQueue<Collision>.ParallelWriter collisions;

        public float bounce;
        public float radiusScale;
        public float maxDiameter;

        public void Execute(ParticleSystemJobData particles, int startIndex, int count)
        {
            var positions = particles.positions;
            var velocities = particles.velocities;
            var sizes = particles.sizes.x;

            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                int particleIndex = sortKeys[i].Index;
                float3 particlePosition = positions[particleIndex];
                float3 particleVelocity = velocities[particleIndex];
                float particleSize = sizes[particleIndex] * 0.5f * radiusScale;
                float particleMass = (4.0f / 3.0f) * math.PI * particleSize * particleSize * particleSize;

                int i2 = i + 1;
                while (i2 < particles.count)
                {
                    int particleIndex2 = sortKeys[i2++].Index;
                    float3 particlePosition2 = positions[particleIndex2];
                    float particleSize2 = sizes[particleIndex2] * 0.5f * radiusScale;
                    float particleSizeSum = particleSize + particleSize2;

                    if (math.distancesq(particlePosition, particlePosition2) < particleSizeSum * particleSizeSum)
                    {
                        float3 particleVelocity2 = velocities[particleIndex2];
                        float particleMass2 = (4.0f / 3.0f) * math.PI * particleSize2 * particleSize2 * particleSize2;
                        
                        // get the mtd
                        float3 delta = particlePosition - particlePosition2;
                        float d = math.length(delta);
                        
                        // minimum translation distance to push particles apart after intersecting
                        float3 mtd = delta * ((particleSizeSum - d) / d);

                        // resolve intersection
                        // inverse mass quantities
                        float im1 = 1.0f / particleMass; 
                        float im2 = 1.0f / particleMass2;

                        // push-pull them apart based off their mass
                        float3 deltaPosition = mtd * (im1 / (im1 + im2));
                        float3 deltaPosition2 = -mtd * (im2 / (im1 + im2));
                        particlePosition += deltaPosition; // apply to cached variable too, to increase stability

                        // impact speed
                        float3 v = (particleVelocity - particleVelocity2);
                        float vn = math.dot(v, math.normalize(mtd));

                        // spheres intersecting but moving away from each other already
                        if (vn > 0.0f)
                            continue;

                        // collision impulse
                        float imp = (-(1.0f + bounce) * vn) / (im1 + im2);
                        float3 impulse = math.normalize(mtd) * imp;

                        // change in momentum
                        float3 deltaVelocity = impulse * im1;
                        float3 deltaVelocity2 = -impulse * im2;

                        // enqueue collision response
                        collisions.Enqueue(new Collision
                        {
                            Index = particleIndex,
                            DeltaPosition = deltaPosition,
                            DeltaVelocity = deltaVelocity,
                            
                            Index2 = particleIndex2,
                            DeltaPosition2 = deltaPosition2,
                            DeltaVelocity2 = deltaVelocity2
                        });
                    }
                    else if (particlePosition2.x - particlePosition.x > maxDiameter)
                    {
                        break;
                    }
                }
            }
        }
    }
    
    [BurstCompile]
    struct ApplyCollisionsJob : IJobParticleSystem
    {
        public NativeQueue<Collision> collisions;
 
        public void Execute(ParticleSystemJobData particles)
        {
            var positions = particles.positions;
            var velocities = particles.velocities;

            int count = collisions.Count;
            for (int i = 0; i < count; i++)
            {
                var collision = collisions.Dequeue();
                
                positions[collision.Index] += (Vector3)collision.DeltaPosition;
                velocities[collision.Index] += (Vector3)collision.DeltaVelocity;
                
                positions[collision.Index2] += (Vector3)collision.DeltaPosition2;
                velocities[collision.Index2] += (Vector3)collision.DeltaVelocity2;
            }
        }
    }
}
