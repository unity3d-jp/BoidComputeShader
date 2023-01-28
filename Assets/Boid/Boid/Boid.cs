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
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
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

    // compute shader file to bind
    public ComputeShader BoidComputeShader;

    // boid properties
    public BoidConfig boidConfig;

    // vfx graph assets to bind
    VisualEffect _boidVisualEffect;
    GraphicsBuffer _boidBuffer;
    int _kernelIndex;

    void OnEnable()
    {
        _boidBuffer = PopulateBoids(boidCount, boidExtent);
        _kernelIndex = BoidComputeShader.FindKernel("CSMain");
        BoidComputeShader.SetBuffer(_kernelIndex, "boidBuffer", _boidBuffer);
        BoidComputeShader.SetInt("numBoids", boidCount);

        _boidVisualEffect = GetComponent<VisualEffect>();
        _boidVisualEffect.SetGraphicsBuffer("Boids", _boidBuffer);
    }

    void OnDisable()
    {
        _boidBuffer?.Dispose();
    }

    void Update()
    {
        UpdateBoids();
    }

    void UpdateBoids()
    {
        var boidTarget = boidConfig.boidTarget != null
            ? boidConfig.boidTarget.position
            : transform.position;
        BoidComputeShader.SetFloat("deltaTime", Time.deltaTime);
        BoidComputeShader.SetFloat("separationWeight", boidConfig.separationWeight);
        BoidComputeShader.SetFloat("alignmentWeight", boidConfig.alignmentWeight);
        BoidComputeShader.SetFloat("targetWeight", boidConfig.targetWeight);
        BoidComputeShader.SetFloat("moveSpeed", boidConfig.moveSpeed);
        BoidComputeShader.SetVector("targetPosition", boidTarget);
        BoidComputeShader.GetKernelThreadGroupSizes(_kernelIndex, out var x, out var y, out var z);
        BoidComputeShader.Dispatch(_kernelIndex, (int) (boidCount / x), 1, 1);
    }

    public static GraphicsBuffer PopulateBoids(int boidCount, float3 boidExtent)
    {
        var random = new Random(256);
        var boidArray =
            new NativeArray<BoidState>(boidCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for (var i = 0; i < boidArray.Length; i++)
        {
            boidArray[i] = new BoidState
            {
                Position = random.NextFloat3(-boidExtent, boidExtent),
                Forward = math.rotate(random.NextQuaternionRotation(), Vector3.forward),
            };
        }
        var boidBuffer =
            new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidArray.Length, Marshal.SizeOf<BoidState>());
        boidBuffer.SetData(boidArray);
        boidArray.Dispose();
        return boidBuffer;
    }
}
