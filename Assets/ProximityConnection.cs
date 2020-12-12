using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ParticleSystemJobs;

public class ProximityConnection : MonoBehaviour
{
    [Min(0.0f)]
    public float distance = 1.0f;
    public Material lineMaterial;
    
    ParticleSystem m_ParticleSystem;
    CacheJob m_CacheJob;
    SortJob m_SortJob;
    ConnectionsJob m_ConnectionsJob;

    NativeArray<SortKey> m_SortKeys;

    Mesh m_Mesh;

    void Start()
    {
        m_ParticleSystem = GetComponent<ParticleSystem>();

        var main = m_ParticleSystem.main;
        int maxParticleCount = main.maxParticles;

        m_SortKeys = new NativeArray<SortKey>(maxParticleCount, Allocator.Persistent);

        m_CacheJob = new CacheJob
        {
            sortKeys = m_SortKeys,
        };
        
        m_SortJob = new SortJob
        {
            sortKeys = m_SortKeys
        };
        
        m_ConnectionsJob = new ConnectionsJob
        {
            sortKeys = m_SortKeys,
            distance = distance
        };

        m_Mesh = new Mesh();
    }

    void OnDisable()
    {
       m_SortKeys.Dispose();
    }

    void OnParticleUpdateJobScheduled()
    {
        var lineList = new NativeQueue<Line>(Allocator.TempJob);
        
        m_ConnectionsJob.lineList = lineList.AsParallelWriter();
        m_ConnectionsJob.distance = distance;
        
        var handle = m_CacheJob.ScheduleBatch(m_ParticleSystem, 2048);
        handle = m_SortJob.Schedule(m_ParticleSystem, handle);
        handle = m_ConnectionsJob.ScheduleBatch(m_ParticleSystem, 1024, handle);
        handle.Complete();
        
        ConvertQueueToMesh(lineList);
        lineList.Dispose();
        
        var matrix = (m_ParticleSystem.main.simulationSpace == ParticleSystemSimulationSpace.World) ? Matrix4x4.identity : gameObject.transform.localToWorldMatrix;
        Graphics.DrawMesh(m_Mesh, matrix, lineMaterial, 0);
    }

    void ConvertQueueToMesh(NativeQueue<Line> lineList)
    {
        int count = lineList.Count;
        var vertices = new Vector3[count * 2];
        var indices = new int[count * 2];
        
        for (int i = 0; i < count; i++)
        {
            var line = lineList.Dequeue();
            var i1 = i * 2;
            var i2 = i1 + 1;

            vertices[i1] = line.Start;
            vertices[i2] = line.End;

            indices[i1] = i1;
            indices[i2] = i2;
        }

        m_Mesh.Clear(true);
        m_Mesh.SetVertices(vertices);
        m_Mesh.SetIndices(indices, MeshTopology.Lines, 0);
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

    struct Line
    {
        public float3 Start;
        public float3 End;
    }
    
    [BurstCompile]
    struct CacheJob : IJobParticleSystemParallelForBatch
    {
        [WriteOnly]
        public NativeArray<SortKey> sortKeys;

        public void Execute(ParticleSystemJobData particles, int startIndex, int count)
        {
            var positionsX = particles.positions.x;

            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
                sortKeys[i] = new SortKey { Key = positionsX[i], Index = i };
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
    struct ConnectionsJob : IJobParticleSystemParallelForBatch
    {
        [ReadOnly]
        public NativeArray<SortKey> sortKeys;
        [WriteOnly]
        public NativeQueue<Line>.ParallelWriter lineList;

        public float distance;

        public void Execute(ParticleSystemJobData particles, int startIndex, int count)
        {
            var positions = particles.positions;

            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                int particleIndex = sortKeys[i].Index;
                float3 particlePosition = positions[particleIndex];

                int i2 = i + 1;
                while (i2 < particles.count)
                {
                    int particleIndex2 = sortKeys[i2++].Index;
                    float3 particlePosition2 = positions[particleIndex2];

                    if (math.distancesq(particlePosition, particlePosition2) < distance * distance)
                    {
                        lineList.Enqueue(new Line
                        {
                            Start = particlePosition,
                            End = particlePosition2
                        });
                    }
                    else if (particlePosition2.x - particlePosition.x > distance)
                    {
                        break;
                    }
                }
            }
        }
    }
}
