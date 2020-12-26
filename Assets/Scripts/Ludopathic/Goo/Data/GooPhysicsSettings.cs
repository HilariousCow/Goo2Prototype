using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GooPhysicsSettings", menuName = "Goo/PhysicsSettings", order = 1)]
public class GooPhysicsSettings : ScriptableObject
{
    [Range(0.5f, 10.0f)]
    public float MaxSpringDistance = 4f;
    [Range(0, 20)]
    public int MaxNearestNeighbours = 12;
    
    [Space]
    [Range(0.0f, 1.0f)]//stubborness
    public float BlobLinearFriction;
    [Range(0.0f, 50.0f)]//stiffness
    public float BlobConstantFriction;
    [Range(0.0f, 100.0f)]
    public float SpringForceConstant = 15f;
}
