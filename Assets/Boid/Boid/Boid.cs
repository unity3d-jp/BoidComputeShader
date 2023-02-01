using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Random = Unity.Mathematics.Random;

[RequireComponent(typeof(VisualEffect))]
public class Boid : MonoBehaviour
{
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)] // Usable as a GraphicsBuffer data type.
    public struct BoidState // boid state data to pack as GraphicsBuffer
    {
        public Vector3 Position;
        public Vector3 Forward;
    }

    [Serializable] // boid properties class
    public class BoidConfig
    {
        public float moveSpeed = 1f;

        [Range(0f, 1f)] public float separationWeight = .5f;

        [Range(0f, 1f)] public float alignmentWeight = .5f;

        [Range(0f, 1f)] public float targetWeight = .5f;

        public Transform boidTarget;
    }

    // number of boids to spawn
    public int boidCount = 32;

    // used to populate boids, used at initialization
    public float3 boidExtent = new(32f, 32f, 32f);

    // compute shader file to dispatch
    public ComputeShader BoidComputeShader;

    // boid properties
    public BoidConfig boidConfig;

    // vfx graph assets to bind
    VisualEffect _boidVisualEffect;
    GraphicsBuffer _boidBuffer;
    int _kernelIndex;

    void OnEnable()
    {
        _boidBuffer = PopulateBoids(boidCount, boidExtent); // initialize boids
        _kernelIndex = BoidComputeShader.FindKernel("CSMain"); // get compute shader kernel id
        BoidComputeShader.SetBuffer(_kernelIndex, "boidBuffer", _boidBuffer); // bind graphics buffer to compute shader
        BoidComputeShader.SetInt("numBoids", boidCount); // bind boid count to compute shader

        _boidVisualEffect = GetComponent<VisualEffect>(); // cache vfx graph component
        _boidVisualEffect.SetGraphicsBuffer("Boids", _boidBuffer); // bind graphics buffer to vfx graph component
    }

    void OnDisable() // dispose buffer on disable
    {
        _boidBuffer?.Dispose();
    }

    void Update()
    {
        UpdateBoids();
    }

    // update boid data, called in Update()
    void UpdateBoids()
    {
        var boidTarget = boidConfig.boidTarget != null
            ? boidConfig.boidTarget.position
            : transform.position; // set boid target position(position to follow?)
        BoidComputeShader.SetFloat("deltaTime", Time.deltaTime); // bind delta time
        BoidComputeShader.SetFloat("separationWeight", boidConfig.separationWeight); // bind separation weight
        BoidComputeShader.SetFloat("alignmentWeight", boidConfig.alignmentWeight); // bind alignment weight
        BoidComputeShader.SetFloat("targetWeight", boidConfig.targetWeight); // bind target weight
        BoidComputeShader.SetFloat("moveSpeed", boidConfig.moveSpeed); // bind move speed
        BoidComputeShader.SetVector("targetPosition", boidTarget); // bind boid target position
        
        // get kernel thread group sizes
        BoidComputeShader.GetKernelThreadGroupSizes(_kernelIndex, out var x, out var y, out var z); 
        // dispatch kernel with x dimension threads
        BoidComputeShader.Dispatch(_kernelIndex, (int) (boidCount / x), 1, 1);
    }

    // spawn boids, called on enable
    public static GraphicsBuffer PopulateBoids(int boidCount, float3 boidExtent)
    {
        var random = new Random(256); // generate random number
        var boidArray = // pack boids into native array, passed to compute shader and vfx graph
            new NativeArray<BoidState>(boidCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        
        // initialize boids
        for (var i = 0; i < boidArray.Length; i++)
        {
            boidArray[i] = new BoidState
            {
                Position = random.NextFloat3(-boidExtent, boidExtent),
                Forward = math.rotate(random.NextQuaternionRotation(), Vector3.forward),
            };
        }
        var boidBuffer = // graphics buffer to pack 'BoidState's
            new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidArray.Length, Marshal.SizeOf<BoidState>());
        boidBuffer.SetData(boidArray); // set GraphicsBuffer data as packed native array
        boidArray.Dispose(); // dispose native array
        return boidBuffer; // return packed GraphicsBuffer
        
        // the returned GraphicsBuffer is disposed in OnDisable().
    }
}
