
using System;
using Ludopathic.Goo.Jobs;

using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Entities;

using KNN;
using KNN.Jobs;

using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.ParticleSystemJobs;

using Random = UnityEngine.Random;

namespace Ludopathic.Goo.Managers
{
   public class BlobManager : MonoBehaviour
   {
      public InputManager InputMan;
      public GameObject BlobPrefab;
      public ParticleSystem BlobParticleSystemOutput;

      public int RandomSeed = 0;
      
      public Camera CameraTransform;

      [Space]
      public bool DynamicallyUpdateNearestNeighbours;
      public bool UseUniqueEdges= true;
      
      
      private bool bNearestNeighboursDirty = true;
      [Space]
      //Gameplay properties
      public float CursorRadius = 10.0f;
      public float CursorAccel = 5.0f;
      public float CursorLinearFriction = 0.1f;
      public float CursorConstantFriction = 0.8f;

      public GooPhysicsSettings GooPhysics;
    
      //Match settings.
      //Move to an SO
      [Space]
      public float PetriDishRadius = 2f;
      public int NumTeams = 2;
      public int NUM_BLOBS = 1000;
      
      //
      // Persistent Entity Memroy
      //
      
      //Cursor properties
      //TODO: a lot of this stuff can be rolled into this concept of components. i.e. there's a lot to share between cursors vs blobs.
      //And we might want cursors to repell one another. So a cursor might just be a fixed ID. Like a negative team or something.
      
      private const int NUM_CURSORS = 1;
      private NativeArray<int> _cursorTeamIDs;
      private NativeArray<float2> _cursorInputDeltas;
      private NativeArray<float2> _cursorAccelerations;
      private NativeArray<float2> _cursorVelocities;
      private NativeArray<float2> _cursorPositions;
      private NativeArray<float> _cursorRadii;
      
      //TODO: effective radii
      
      //Cursor display
      private Transform[] _cursorOutputTransforms;
      private TransformAccessArray _cursorTransformAccessArray;
      
      //Goo/Blob Properties.
      private NativeArray<int> _blobTeamIDs;
      private NativeArray<int> _blobGroupIDs;
      private NativeArray<float> _blobRadii;
      private NativeArray<float2> _blobAccelerations;
      private NativeArray<float2> _blobVelocities;
      private NativeArray<float2> _blobPositions;
      private NativeArray<Color> _blobColors;
      
      private NativeArray<float3> _blobPositionsV3;

      private NativeQueue<int> _floodQueue;
      private NativeArray<int> _numGroups;//Just an output. Can't get output without that native rapper!
      private NativeArray<Bounds> _overallGooBounds;
      
      
      public Bounds OverallGooBounds;
      
      
      //Goo Graph
      //think about slices for each blob which is just other-nearby-blobs. But have to remember their master index
   
      private NativeArray<RangeQueryResult> _blobKNNNearestNeighbourQueryResults;
      
      private NativeMultiHashMap<int, int> _uniqueBlobEdges;
      
      public NativeHashSet<long> _uniqueBlobEdgesHashSet;
      
      //
      //Job Data
      //
      private MemsetNativeArray<float2> _jobDataResetBlobAccelerations;
      private MemsetNativeArray<float2> _jobDataResetCursorAccelerations;
      private MemsetNativeArray<int> _jobDataResetGooGroups;//what if we don't totally flush this every time? Maybe identify grouped blobs that are no longer connected to their established group?
      
      private MemsetNativeArray<float> _jobDataCopyBlobRadii;
      private JobCopyBlobInfoToFloat3 _jobDataCopyBlobInfoToFloat3;
      
      
      private KnnContainer _knnContainer;
      private KnnRebuildJob _jobBuildKnnTree;
      
      private QueryRangesBatchJob _jobDataQueryNearestNeighboursKNN;
      private JobCompileUniqueEdges _jobCompileDataUniqueEdges;

      private JobFloodFillIDs _jobDataFloodFillGroupIDs;//need to make this per team
      
      //either or
      private JobSpringForceUsingKNNResults _jobDataSpringForcesUsingKnn;
      private JobUniqueSpringForce _jobDataSpringForcesUniqueEdges;
      // 
      
      
      private JobVelocityInfluenceFalloff _jobDataFluidInfluence;
      
      private JobSetAcceleration _jobDataSetCursorAcceleration;
      private JobApplyLinearAndConstantFriction _jobDataApplyCursorFriction;
      private JobApplyAcceelrationAndVelocity _jobDataUpdateCursorPositions;
      private JobCursorsInfluenceBlobs _jobDataCursorsInfluenceBlobs;
      private JobApplyLinearAndConstantFriction _jobDataApplyFrictionToBlobs;
      private JobApplyAcceelrationAndVelocity _jobDataUpdateBlobPositions;


      private JobDebugColorisationInt _jobDataDebugColorisationInt;
      //private JobDebugColorisationFloat _jobDataDebugColorisationFloat;//as yet unused
      private JobDebugColorisationFloat2XY _jobDataDebugColorisationFloat2Magnitude;
      
      
      private JobDebugColorisationKNNRangeQuery _jobDataDebugColorisationKNNLength;
      
      
      private JopCopyBlobsToParticleSystem _jobDataCopyBlobsToParticleSystem;
      private JobCopyBlobsToTransforms _jobDataCopyCursorsToTransforms;

      private JobCalculateAABB _jobDataCalculateAABB;
      
      //
      //Job Handles (probably don't need to be members)
      //
      
      private int _GameFrame = 0;
      private JobHandle _jobHandleResetBlobAccelerations;
      private JobHandle _jobHandleResetCursorAccelerations;
      private JobHandle _jobHandleResetGroupIDs;
      private JobHandle _jobHandleSetBlobRadii;
   
      private JobHandle _jobHandleResetJobs;//combiner
      private JobHandle _jobCopy2DArrayTo3DArray;//TODO




      private JobHandle _jobHandleCopyBlobInfoToFloat3;
      private JobHandle _jobHandleBuildKNNTree;

      private JobHandle _jobHandleFloodFillGroupiID;
      private JobHandle _jobHandleSpringForces;
      
      private JobHandle _jobHandleSetCursorAcceleration;
      private JobHandle _jobHandleApplyCursorFriction;
      private JobHandle _jobHandleUpdateCursorPositions;
      private JobHandle _jobHandleCursorsInfluenceBlobs;
      private JobHandle _jobHandleApplyBlobFriction;
      private JobHandle _jobHandleFluidInfluences;
      private JobHandle _jobHandleUpdateBlobPositions;
      private JobHandle _jobHandleCopyCursorsToTransforms;
      private JobHandle _jobHandleCopyBlobsToParticleSystem;
      private JobHandle _jobHandleBuildAABB;

      
      //Debug vis
      public enum BlobColorDebugStyle
      {
         Edges,
         Velocity,
         Acceleration,
         TeamID,
         GroupID
      }

      
      //Debug drawing.
      public BlobColorDebugStyle DebugStyle;
      
      private void OnEnable()
      {
         Application.targetFrameRate = 600;
         
         Random.InitState(RandomSeed);
         
         _cursorTeamIDs = new NativeArray<int>(NUM_CURSORS, Allocator.Persistent);
         _cursorInputDeltas = new NativeArray<float2>(NUM_CURSORS, Allocator.Persistent);
         _cursorAccelerations= new NativeArray<float2>(NUM_CURSORS, Allocator.Persistent);
         _cursorVelocities= new NativeArray<float2>(NUM_CURSORS, Allocator.Persistent);
         _cursorPositions= new NativeArray<float2>(NUM_CURSORS, Allocator.Persistent);
         _cursorRadii= new NativeArray<float>(NUM_CURSORS, Allocator.Persistent);

         for (int index = 0; index < NUM_CURSORS; index++)
         {
            _cursorTeamIDs[index] = index % NUM_CURSORS;
            _cursorInputDeltas[index] = float2.zero;
            _cursorAccelerations[index] =  float2.zero;
            _cursorVelocities[index] = float2.zero;
            _cursorPositions[index] = Random.insideUnitCircle;
            _cursorRadii[index] = CursorRadius;
         }
         
         _cursorOutputTransforms = new Transform[NUM_CURSORS];
         //Cursor output things
         for (int index = 0; index < NUM_CURSORS; index++)
         {
            GameObject blobInstance = Instantiate(BlobPrefab);
            blobInstance.name = $"Cursor{index}";
            blobInstance.transform.rotation = Quaternion.Euler(90f,0f,0f);
            blobInstance.transform.localScale = Vector3.one * _cursorRadii[index];
            _cursorOutputTransforms[index] = blobInstance.transform;
            
            
         }
         _cursorTransformAccessArray = new TransformAccessArray(_cursorOutputTransforms);

         InitBlobData(NUM_BLOBS, BlobParticleSystemOutput);

      }

      private void InitBlobData(int numBlobs, ParticleSystem blobParticleSystemOutput)
      {
         var main = BlobParticleSystemOutput.main;
         main.maxParticles = NUM_BLOBS;
         BlobParticleSystemOutput.Emit(NUM_BLOBS);
         //Blob enabling
     
         //create scratch data
         _blobVelocities = new NativeArray<float2>(NUM_BLOBS, Allocator.Persistent);
         _blobPositions = new NativeArray<float2>(NUM_BLOBS, Allocator.Persistent);
         _blobAccelerations = new NativeArray<float2>(NUM_BLOBS, Allocator.Persistent);
         _blobRadii = new NativeArray<float>(NUM_BLOBS, Allocator.Persistent);
         _blobTeamIDs = new NativeArray<int>(NUM_BLOBS, Allocator.Persistent);
         _blobGroupIDs = new NativeArray<int>(NUM_BLOBS, Allocator.Persistent);
         _numGroups = new NativeArray<int>(1, Allocator.Persistent);
         _floodQueue = new NativeQueue<int>(Allocator.Persistent);
         _blobColors = new NativeArray<Color>(NUM_BLOBS, Allocator.Persistent);
         _blobPositionsV3 = new NativeArray<float3>(NUM_BLOBS, Allocator.Persistent);
         _overallGooBounds = new NativeArray<Bounds>(1, Allocator.Persistent);
         _overallGooBounds[0] = OverallGooBounds;
         
         //copy init values into scratch data
         for (int index = 0; index < NUM_BLOBS; index++)
         {
            Vector3 randomPos = Random.insideUnitCircle * PetriDishRadius;

            float angleFraction =( Mathf.Atan2(randomPos.x, randomPos.y) + Mathf.PI) / (Mathf.PI * 2.0f);//flipped x and y is intentional so that 2player gets horizontal split
            //Vector3 randomVel = Random.insideUnitCircle;
            _blobTeamIDs[index] = Mathf.FloorToInt( angleFraction * (float)NumTeams );
            _blobGroupIDs[index] = -1;
            _blobPositions[index] = new float2(randomPos.x, randomPos.y);
            _blobVelocities[index] = float2.zero;
            _blobAccelerations[index] =  float2.zero;
            _blobColors[index] = Color.magenta;
            _blobPositionsV3[index] = new float3(randomPos.x, randomPos.y, _blobTeamIDs[index]);
            _blobRadii[index] = GooPhysics.MaxSpringDistance;
         }

      
         //output things
       

         main = BlobParticleSystemOutput.main;
         main.maxParticles = NUM_BLOBS;
         
     
         _knnContainer = new KnnContainer(_blobPositionsV3, false, Allocator.Persistent);
         
    
         
         InitJobData();
      }


      private void Update()
      {
         InputMan.PollAllInputs(_GameFrame, 10);

         //todo: update all these properties

       
         
         //OverallGooBounds = new Bounds(Vector3.zero, Vector3.one * float.MinValue);
         //_jobDataEncapsulateAABB.AABBBounds = OverallGooBounds;
         
         
         
         
         UpdateSimulation(Mathf.Min(1f/60f, Time.deltaTime) );
         _GameFrame++;
      }

      private void InitJobData()
      {

         float deltaTime = 0f;
         #region JobDataSetup
         //
         //Init all job data here. Declare roughly inline. Optional brackets for things that can be parallel
         //

         #region ResetBeginningOfSimFrame

         _jobDataResetBlobAccelerations = new MemsetNativeArray<float2> {Source = _blobAccelerations, Value = float2.zero};
         
         _jobDataResetCursorAccelerations = new MemsetNativeArray<float2> {Source = _cursorAccelerations, Value = float2.zero};

         _jobDataResetGooGroups = new MemsetNativeArray<int> {Source = _blobGroupIDs, Value = -1};
         _jobDataCopyBlobRadii = new MemsetNativeArray<float> {Source = _blobRadii, Value = GooPhysics.MaxSpringDistance};
         
         _jobDataCopyBlobInfoToFloat3 = new JobCopyBlobInfoToFloat3
         {
            BlobPos = _blobPositions,
            BlobTeams = _blobTeamIDs,
            BlobPosFloat3 = _blobPositionsV3
         };


         _jobBuildKnnTree = new KnnRebuildJob(_knnContainer);

         // Initialize all the range query results
         _blobKNNNearestNeighbourQueryResults = new NativeArray<RangeQueryResult>(_blobPositions.Length, Allocator.Persistent);

         _uniqueBlobEdges = new NativeMultiHashMap<int, int>(_blobPositions.Length * 40, Allocator.Persistent);
         _uniqueBlobEdgesHashSet = new NativeHashSet<long>(_blobPositions.Length* 40, Allocator.Persistent);
         // Each range query result object needs to declare upfront what the maximum number of points in range is
         
         
         for (int i = 0; i < _blobKNNNearestNeighbourQueryResults.Length; ++i) 
         {
            _blobKNNNearestNeighbourQueryResults[i] = new RangeQueryResult(GooPhysics.MaxNearestNeighbours, Allocator.Persistent);
         }

         
         _jobDataQueryNearestNeighboursKNN = new QueryRangesBatchJob{ 
            m_container = _knnContainer,
            m_queryPositions = _blobPositionsV3, 
            m_queryRadii = _blobRadii,
            Results = _blobKNNNearestNeighbourQueryResults
            
         };
         #endregion //ResetBeginningOfSimFrame

         #region Updates
         //build edges with existing positions


         _jobDataFloodFillGroupIDs = new JobFloodFillIDs()
         {
            BlobNearestNeighbours = _blobKNNNearestNeighbourQueryResults,
            GroupIDs = _blobGroupIDs,
            FloodQueue = _floodQueue,
            NumGroups = _numGroups //for safety.don't want divide by zero
         };
         
         _jobDataSpringForcesUsingKnn = new JobSpringForceUsingKNNResults
         {
            AccelerationAccumulator = _blobAccelerations,
            BlobNearestNeighbours = _blobKNNNearestNeighbourQueryResults,
            MaxEdgeDistanceRaw = GooPhysics.MaxSpringDistance /* * 2.0f*/,
            SpringConstant = GooPhysics.SpringForce,
            DampeningConstant = GooPhysics.DampeningConstant,
            Positions = _blobPositions,
            Velocity = _blobVelocities,
         };

          _jobCompileDataUniqueEdges = new JobCompileUniqueEdges
         {
            BlobNearestNeighbours = _blobKNNNearestNeighbourQueryResults,
            Edges = _uniqueBlobEdges.AsParallelWriter(),
            UniqueEdges = _uniqueBlobEdgesHashSet.AsParallelWriter()
         };
         
         _jobDataSpringForcesUniqueEdges = new JobUniqueSpringForce
         {
            AccelerationAccumulator = _blobAccelerations,
            Springs = _uniqueBlobEdges,
            MaxEdgeDistanceRaw = GooPhysics.MaxSpringDistance /* * 2.0f*/,
            SpringConstant = GooPhysics.SpringForce,
            DampeningConstant = GooPhysics.DampeningConstant,
            Positions = _blobPositions,
            Velocity = _blobVelocities,
         };
         
         _jobDataFluidInfluence = new JobVelocityInfluenceFalloff
         {
            BlobPositions = _blobPositions,
            BlobVelocities = _blobVelocities,
            BlobNearestNeighbours = _blobKNNNearestNeighbourQueryResults,
            InfluenceRadius = _blobRadii,
            InfluenceModulator =  GooPhysics.FluidInfluenceModulator,
            BlobAccelAccumulator = _blobAccelerations
         };

         //update cursor accel based on inputs
         //todo: could be CopyTo?
         
         //_cursorInputDeltas.CopyTo(_cursorAccelerations);
         _jobDataSetCursorAcceleration = new JobSetAcceleration
         {
            ValueToSet = _cursorInputDeltas,
            AccumulatedAcceleration = _cursorAccelerations
         };

         //update cursor friction
         _jobDataApplyCursorFriction = new JobApplyLinearAndConstantFriction
         {
            DeltaTime = deltaTime,
            LinearFriction = CursorLinearFriction,
            ConstantFriction = CursorConstantFriction,
            AccumulatedAcceleration = _cursorAccelerations,
            Velocity = _cursorVelocities
         };


         _jobDataUpdateCursorPositions = new JobApplyAcceelrationAndVelocity
         {
            DeltaTime = deltaTime,
            AccumulatedAcceleration = _cursorAccelerations,
            VelocityInAndOut = _cursorVelocities,
            PositionInAndOut = _cursorPositions
         };

         //Now we can update the blobs with the new state of the cursors
         _jobDataCursorsInfluenceBlobs = new JobCursorsInfluenceBlobs
         {
            CursorPositions = _cursorPositions,
            CursorVelocities = _cursorVelocities,
            CursorRadius = _cursorRadii,
            BlobPositions = _blobPositions, 
            BlobAccelAccumulator = _blobAccelerations
         };

         _jobDataApplyFrictionToBlobs = new JobApplyLinearAndConstantFriction
         {
            DeltaTime = deltaTime,
            //TODO: maybe I want friction based on acceleration (t*t) since that's the freshest part of this.
            //So, constant + linear(t) + accelerative (t*t)
            LinearFriction = GooPhysics.LinearFriction,
            ConstantFriction = GooPhysics.ConstantFriction,
            AccumulatedAcceleration = _blobAccelerations,
            Velocity = _blobVelocities
         };

         //Blob sim gets updated
         _jobDataUpdateBlobPositions = new JobApplyAcceelrationAndVelocity
         {
            DeltaTime = deltaTime,
            AccumulatedAcceleration = _blobAccelerations,
            VelocityInAndOut = _blobVelocities,
            PositionInAndOut = _blobPositions
         };

         //Output

         _jobDataDebugColorisationInt = new JobDebugColorisationInt()
         {
            minVal = 0,
            maxVal = 10,
            values = _blobGroupIDs,
            colors = _blobColors,
         };

         _jobDataDebugColorisationKNNLength = new JobDebugColorisationKNNRangeQuery()
         {
            minVal = 0,
            maxVal = 10,
            values = _blobKNNNearestNeighbourQueryResults,
            colors = _blobColors,
         };
         
       /*  _jobDataDebugColorisationFloat = new JobDebugColorisationFloat
         {
            minVal = 0,
            maxVal = 10,
            values = _blobEdgeCount,
            colors =_blobColors,
         }*/

      _jobDataDebugColorisationFloat2Magnitude = new JobDebugColorisationFloat2XY
      {
          maxVal = 10,
          values = _blobVelocities,
          colors = _blobColors
      };
             
      _jobDataCopyBlobsToParticleSystem = new JopCopyBlobsToParticleSystem
      {
         colors =  _blobColors,
         positions = _blobPositions,
         velocities = _blobVelocities
      };
      
      _jobDataCopyCursorsToTransforms = new JobCopyBlobsToTransforms
      {
         BlobPos = _cursorPositions
      };


      #region BoundsForCamera
      _jobDataCalculateAABB = new JobCalculateAABB()
      {
         Positions = _blobPositions,
         Bounds = _overallGooBounds
      };
      #endregion // BoundsForCamera
      #endregion // Updates
      #endregion // JobDataSetup

      }


      void UpdateRuntimeValues(float deltaTime)
      {
         //update cursor friction
         _jobDataApplyCursorFriction.DeltaTime = deltaTime;
         _jobDataUpdateCursorPositions.DeltaTime = deltaTime;
         _jobDataApplyFrictionToBlobs.DeltaTime = deltaTime;
         _jobDataUpdateBlobPositions.DeltaTime = deltaTime;

         _jobDataCopyBlobRadii.Value = GooPhysics.MaxSpringDistance;
         
         _jobDataApplyFrictionToBlobs.ConstantFriction = GooPhysics.ConstantFriction;
         _jobDataApplyFrictionToBlobs.LinearFriction = GooPhysics.ConstantFriction;
         
         //_jobDataQueryNearestNeighboursKNN.m_range = GooPhysics.MaxSpringDistance;//replace with a full copy job

         bool needsReallocation = GooPhysics.MaxNearestNeighbours != _blobKNNNearestNeighbourQueryResults[0].m_capacity;

         if (needsReallocation)
         {
            Debug.Log("Reallocating knn queries: " + GooPhysics.MaxNearestNeighbours);
            for (int i = 0; i < _blobKNNNearestNeighbourQueryResults.Length; ++i)
            {
               _blobKNNNearestNeighbourQueryResults[i].Dispose();
            }
            
            for (int i = 0; i < _blobKNNNearestNeighbourQueryResults.Length; ++i)
            {
               _blobKNNNearestNeighbourQueryResults[i] =
                  new RangeQueryResult(GooPhysics.MaxNearestNeighbours, Allocator.Persistent);
            }
        
            bNearestNeighboursDirty = true;
         }
         _jobDataSpringForcesUsingKnn.SpringConstant = GooPhysics.SpringForce;
         _jobDataSpringForcesUsingKnn.DampeningConstant = GooPhysics.DampeningConstant;
         _jobDataSpringForcesUsingKnn.MaxEdgeDistanceRaw = GooPhysics.MaxSpringDistance /* * 2.0f*/;
         
         
         _jobDataSpringForcesUniqueEdges.SpringConstant = GooPhysics.SpringForce;
         _jobDataSpringForcesUniqueEdges.DampeningConstant = GooPhysics.DampeningConstant;
         _jobDataSpringForcesUniqueEdges.MaxEdgeDistanceRaw = GooPhysics.MaxSpringDistance /* * 2.0f*/;
         
   
            
         _jobDataFluidInfluence.InfluenceModulator = GooPhysics.FluidInfluenceModulator;

       
      }
      
      private void UpdateSimulation(float deltaTime)
      {
         
         //todo: break this down better. use delta time plus last sim time to figure out a list of game frames to step through, using a starting state.
         for (int index = 0; index < NUM_CURSORS; index++)
         {
            //_cursorTeamIDs[index] = index;
            _cursorInputDeltas[index] = InputMan.ListOfSources[index].GetInputAxis() * CursorAccel;//todo: needs a game frame to reference
            //_cursorAccelerations[index] =  float2.zero;
            //_cursorVelocities[index] = float2.zero;
            //_cursorPositions[index] = Random.insideUnitCircle;
            //_cursorRadii[index] = 1.0f;
         }

         UpdateRuntimeValues( deltaTime );
         
        
         #region Job Kickoff and Dependancy
         //
         // Fire off jobs with all the data that has been set up above. Prefer not to in-line job data and job scheduling due to dependancies
         //
         
         #region ResetBeginningOfSimFrame
         _jobHandleResetBlobAccelerations = _jobDataResetBlobAccelerations.Schedule(_blobAccelerations.Length, 64);
         _jobHandleResetCursorAccelerations = _jobDataResetCursorAccelerations.Schedule(_cursorAccelerations.Length, 1);
         _jobHandleResetGroupIDs = _jobDataResetGooGroups.Schedule(_blobGroupIDs.Length, 64);
         
         #endregion //ResetBeginningOfSimFrame
         
         //_jobHandleResetJobs.Complete();

        
         //We need to copy values of positions over into the knn tree (one day we might be able to rule this out)
       
         
         _jobHandleResetJobs = JobHandle.CombineDependencies(_jobHandleResetBlobAccelerations, _jobHandleResetCursorAccelerations, _jobHandleResetGroupIDs );
         //_jobDataQueryNearestNeighboursKNN
         
         if (bNearestNeighboursDirty || DynamicallyUpdateNearestNeighbours) //HACK: see what happens when we maintain the initial lattice
         {
            
            
            #region KNN Tree
            _jobHandleCopyBlobInfoToFloat3 = _jobDataCopyBlobInfoToFloat3.Schedule(_blobPositionsV3.Length, 64);
            _jobHandleBuildKNNTree = _jobBuildKnnTree.Schedule(_jobHandleCopyBlobInfoToFloat3);
            _jobHandleSetBlobRadii = _jobDataCopyBlobRadii.Schedule(_blobRadii.Length, 64);
            
            JobHandle jobHandleResetRadiiAndBuildKNNTree =  JobHandle.CombineDependencies(_jobHandleSetBlobRadii, _jobHandleBuildKNNTree);
            
            //now query nearest neighbours
            JobHandle jobHandleQueryKNN = _jobDataQueryNearestNeighboursKNN.Schedule(_blobPositionsV3.Length, 64, jobHandleResetRadiiAndBuildKNNTree);
            _jobHandleResetJobs = JobHandle.CombineDependencies(_jobHandleResetJobs, jobHandleQueryKNN);
            #endregion


            if (UseUniqueEdges)
            {
               Debug.Log($"Unique Blob edges length { _uniqueBlobEdges.Count() }");
               _uniqueBlobEdgesHashSet.Clear();
               _uniqueBlobEdges.Clear();
               //_uniqueBlobEdges.Clear();
               //_uniqueBlobEdges.Dispose();
               //_uniqueBlobEdges = new NativeMultiHashMap<int, int>(_blobPositions.Length * 40, Allocator.Persistent);
               JobHandle jobHandFindUniqueEdges = _jobCompileDataUniqueEdges.Schedule(_blobPositionsV3.Length, 64, _jobHandleResetJobs);
               
               _jobHandleResetJobs = JobHandle.CombineDependencies(_jobHandleResetJobs, jobHandFindUniqueEdges);
            }
            
            bNearestNeighboursDirty = false;
         }
       

         #region SimUpdateFrame
         //
         // Cursors must be done first. Luckily there's very few
         //
         
      
         //update cursors//todo: treat more like ECS. cursors happen to have positions/velocities/radii. But they out to be "type" tagged somehow.
         
         _jobHandleSetCursorAcceleration = _jobDataSetCursorAcceleration.Schedule(_cursorInputDeltas.Length, 1, _jobHandleResetJobs);
         
         _jobHandleApplyCursorFriction = _jobDataApplyCursorFriction.Schedule(_cursorInputDeltas.Length, 1, _jobHandleSetCursorAcceleration);
         
         _jobHandleUpdateCursorPositions = _jobDataUpdateCursorPositions.Schedule(_cursorInputDeltas.Length, 1, _jobHandleApplyCursorFriction);
         
         _jobHandleCursorsInfluenceBlobs = _jobDataCursorsInfluenceBlobs.Schedule(_blobPositions.Length, 64, _jobHandleUpdateCursorPositions);//todo: give cursors knnquery data.
         
         //Cursor Influences blobs once it's ready
         //Blob sim gets updated after cursor influence
         
         //blobs all figure out how much push and pull is coming from neighbouring blobs.

         _jobHandleFloodFillGroupiID = _jobDataFloodFillGroupIDs.Schedule(_jobHandleCursorsInfluenceBlobs);
            
         
         //Do the below really rely on the group ids? Not yet, but we might find a reason for them to?


         if (UseUniqueEdges)
         {
            _jobHandleSpringForces = _jobDataSpringForcesUniqueEdges.Schedule(_blobAccelerations.Length, 64, _jobHandleFloodFillGroupiID);
         }
         else
         {
            _jobHandleSpringForces = _jobDataSpringForcesUsingKnn.Schedule(_blobAccelerations.Length, 64, _jobHandleFloodFillGroupiID);
         }


         _jobHandleApplyBlobFriction = _jobDataApplyFrictionToBlobs.Schedule(_blobAccelerations.Length, 64, _jobHandleSpringForces);
         _jobHandleFluidInfluences =  _jobDataFluidInfluence.Schedule(_blobAccelerations.Length, 64, _jobHandleApplyBlobFriction);

         _jobHandleUpdateBlobPositions = _jobDataUpdateBlobPositions.Schedule(_blobAccelerations.Length, 64, _jobHandleFluidInfluences);
         
         #endregion //SimUpdateFrame
      
         //temp - needs an interpolator job
         
         //Todo: spit out into a particle effect instead of transforms, which are probably slow as heck
         //but this is still somewhat useful for debug

         JobHandle jobHandleDebugColorization;

         switch (DebugStyle)
         {
            case BlobColorDebugStyle.Edges:
               jobHandleDebugColorization =
                  _jobDataDebugColorisationKNNLength.Schedule(_blobKNNNearestNeighbourQueryResults.Length, 64, _jobHandleUpdateBlobPositions);
                 
               break;
            case BlobColorDebugStyle.Velocity:
               jobHandleDebugColorization =
                  _jobDataDebugColorisationFloat2Magnitude.Schedule(_blobVelocities.Length, 64, _jobHandleUpdateBlobPositions);
               break;
            case BlobColorDebugStyle.Acceleration:
               jobHandleDebugColorization =
                  _jobDataDebugColorisationFloat2Magnitude.Schedule(_blobAccelerations.Length, 64, _jobHandleUpdateBlobPositions);
               break;
            case BlobColorDebugStyle.TeamID:
               jobHandleDebugColorization =
                  _jobDataDebugColorisationInt.Schedule(_blobTeamIDs.Length, 64, _jobHandleUpdateBlobPositions);
               break;
            case BlobColorDebugStyle.GroupID:
               jobHandleDebugColorization =
                  _jobDataDebugColorisationInt.Schedule(_blobGroupIDs.Length, 64, _jobHandleUpdateBlobPositions);
               break;
            default:
               throw new ArgumentOutOfRangeException();
         }
         
         
         _jobHandleCopyBlobsToParticleSystem = _jobDataCopyBlobsToParticleSystem.ScheduleBatch(BlobParticleSystemOutput, 64, jobHandleDebugColorization);
         _jobHandleCopyCursorsToTransforms = _jobDataCopyCursorsToTransforms.Schedule(_cursorTransformAccessArray, _jobHandleCursorsInfluenceBlobs);
         
         _jobHandleCopyBlobsToParticleSystem.Complete();
         _jobHandleCopyCursorsToTransforms.Complete();

         _jobHandleBuildAABB = _jobDataCalculateAABB.Schedule(   _jobHandleUpdateBlobPositions);
         _jobHandleBuildAABB.Complete();
         
         
//         Debug.Log($"Unique Blob edges length { _uniqueBlobEdges.Count() }");
         #endregion // Job Kickoff and Dependancy

       
         
         //No. You must call "complete" on any handle that has something dependant on it. Which is all of them, you'd expect.
         //maybe i only need to complete the last, since that's dependant.
      }

      public Gradient EdgeBlobGradient;
      private void LateUpdate()
      {
         UpdateBlobColors(ref _blobColors);//wants to be a job tbh.
        
         //_jobSplat2.Complete();//force this job to hold up the thread until complete
         //also, looks like you can complete this in late update.
         //also, you only have to complete the job of last job in a chain.

      
         OverallGooBounds = _overallGooBounds[0];//job data will have changed this.
      }

      //slow assed. Can jobify
      private void UpdateBlobColors(ref NativeArray<Color> colors)
      {
         switch (DebugStyle)
         {
            case BlobColorDebugStyle.Edges:
               _jobDataDebugColorisationKNNLength.minVal = 0;
               _jobDataDebugColorisationKNNLength.maxVal = GooPhysics.MaxNearestNeighbours;
               _jobDataDebugColorisationKNNLength.values = _blobKNNNearestNeighbourQueryResults;
               break;
            case BlobColorDebugStyle.Velocity:
               _jobDataDebugColorisationFloat2Magnitude.maxVal = 1f;
               _jobDataDebugColorisationFloat2Magnitude.values = _blobVelocities;
               break;
            case BlobColorDebugStyle.Acceleration:
               _jobDataDebugColorisationFloat2Magnitude.maxVal = GooPhysics.SpringForce;
               _jobDataDebugColorisationFloat2Magnitude.values = _blobAccelerations;
               break;
            case BlobColorDebugStyle.TeamID:
               _jobDataDebugColorisationInt.minVal = 0;
               _jobDataDebugColorisationInt.maxVal = 5;
               _jobDataDebugColorisationInt.values = _blobTeamIDs;
               break;
            case BlobColorDebugStyle.GroupID:
               _jobDataDebugColorisationInt.minVal = 0;
               _jobDataDebugColorisationInt.maxVal = _numGroups[0];//funky, but it's the only way to get an int out of the job
               _jobDataDebugColorisationInt.values = _blobGroupIDs;
               break;
            default:
               throw new ArgumentOutOfRangeException();
         }
      }
   
    
      private void OnDisable()
      {
         //All jobs must have .Complete called on them before trying to dispose any.
         
         //ALL native arrays will complain if jobs are not successully completed.
         
         //destroy scratch data
         //The "is created" is necessary because on disable may be called on this object even when it is not enabled (i.e. when application closest)
         
         
         if(_cursorTeamIDs.IsCreated) _cursorTeamIDs.Dispose();
         if(_cursorInputDeltas .IsCreated) _cursorInputDeltas.Dispose();
         if(_cursorAccelerations.IsCreated) _cursorAccelerations.Dispose();
         if(_cursorVelocities.IsCreated) _cursorVelocities.Dispose();
         if(_cursorPositions.IsCreated) _cursorPositions.Dispose();
         if(_cursorRadii.IsCreated) _cursorRadii.Dispose();
         if(_blobPositionsV3.IsCreated) _blobPositionsV3.Dispose();
         if(_blobVelocities.IsCreated) _blobVelocities.Dispose();
         if(_blobPositions .IsCreated) _blobPositions.Dispose();
         if(_blobAccelerations.IsCreated) _blobAccelerations.Dispose();
         if(_blobTeamIDs.IsCreated) _blobTeamIDs.Dispose();
         if(_blobGroupIDs.IsCreated) _blobGroupIDs.Dispose();
         if(_numGroups.IsCreated) _numGroups.Dispose();
         if(_floodQueue.IsCreated) _floodQueue.Dispose();
         if(_blobColors.IsCreated) _blobColors.Dispose();

         if (_uniqueBlobEdges.IsCreated) _uniqueBlobEdges.Dispose();
         
         _knnContainer.Dispose();
         foreach (var result in _blobKNNNearestNeighbourQueryResults) {
            result.Dispose();
         }
     
      }
   }
}