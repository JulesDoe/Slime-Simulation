﻿using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;

public class Simulation3 : MonoBehaviour
{
    public enum SpawnMode { Random, Point, InwardCircle, RandomCircle }

    public MeshRenderer trailMapDebug;
    public MeshRenderer diffusedTrailMapDebug;
    public MeshRenderer displayTextureDebug;
    public MeshRenderer parameterMap1Debug;
    public MeshRenderer parameterMap2Debug;
    public MeshRenderer colorMapDebug;
    public Camera colorMapCamer;
    public Camera parameterMap1Camera;
    public Camera parameterMap2Camera;
    RenderTexture parameterMap1;
    RenderTexture parameterMap2;
    RenderTexture colorMap;
    const int updateKernel = 0;
    const int diffuseMapKernel = 1;
	
    const int colourKernel = 2;

    public ComputeShader compute;
    public ComputeShader drawAgentsCS;

    public SlimeSettings settings;

    [Header("Display Settings")]
    public bool showAgentsOnly;
    public FilterMode filterMode = FilterMode.Point;
    public GraphicsFormat format = ComputeHelper.defaultGraphicsFormat;


    [SerializeField, HideInInspector] protected RenderTexture trailMap;
    [SerializeField, HideInInspector] protected RenderTexture diffusedTrailMap;
    [SerializeField, HideInInspector] protected RenderTexture displayTexture;

    ComputeBuffer agentBuffer;
    ComputeBuffer settingsBuffer;
    Texture2D colourMapTexture;

    protected virtual void Start()
    {
        Init();
        trailMapDebug.material.mainTexture = trailMap;
        diffusedTrailMapDebug.material.mainTexture = diffusedTrailMap;
        displayTextureDebug.material.mainTexture = displayTexture;

        colorMapCamer.targetTexture = colorMap;
        parameterMap1Camera.targetTexture = parameterMap1;
        parameterMap2Camera.targetTexture = parameterMap2;

        parameterMap1Debug.material.mainTexture = parameterMap1;
        parameterMap2Debug.material.mainTexture = parameterMap2;
        colorMapDebug.material.mainTexture = colorMap;
    }


    void Init()
    {
        // Create render textures
        ComputeHelper.CreateRenderTexture(ref trailMap, settings.width, settings.height, filterMode, format);
        ComputeHelper.CreateRenderTexture(ref diffusedTrailMap, settings.width, settings.height, filterMode, format);
        ComputeHelper.CreateRenderTexture(ref displayTexture, settings.width, settings.height, filterMode, format);

        int parameterMapWidth = settings.width / settings.parameterMapSubSamplingFactor ;
        int parameterMapHeight = settings.height / settings.parameterMapSubSamplingFactor ;
        ComputeHelper.CreateRenderTexture(ref parameterMap1, parameterMapWidth, parameterMapHeight, filterMode, format);
        ComputeHelper.CreateRenderTexture(ref parameterMap2, parameterMapWidth, parameterMapHeight, filterMode, format);
        ComputeHelper.CreateRenderTexture(ref colorMap,      parameterMapWidth, parameterMapHeight, filterMode, format);


        // Assign textures
        compute.SetTexture(updateKernel, "TrailMap", trailMap);
        compute.SetTexture(updateKernel, "ParameterMap1", parameterMap1);
        compute.SetTexture(updateKernel, "ParameterMap2", parameterMap2);
        compute.SetTexture(updateKernel, "ColorMap", colorMap);
        compute.SetTexture(diffuseMapKernel, "TrailMap", trailMap);
        compute.SetTexture(diffuseMapKernel, "DiffusedTrailMap", diffusedTrailMap);
        compute.SetTexture(colourKernel, "ColourMap", displayTexture);
        compute.SetTexture(colourKernel, "TrailMap", trailMap);

        // Create agents with initial positions and angles
        Agent[] agents = new Agent[settings.numAgents];
        for (int i = 0; i < agents.Length; i++)
        {
            Vector2 centre = new Vector2(settings.width / 2, settings.height / 2);
            Vector2 startPos = Vector2.zero;
            float randomAngle = Random.value * Mathf.PI * 2;
            float angle = 0;


            startPos = new Vector2(Random.Range(0, settings.width), Random.Range(0, settings.height));
            angle = randomAngle;


            Vector3Int speciesMask;
            int speciesIndex = 0;
            int numSpecies = settings.speciesSettings.Length;

            if (numSpecies == 1)
            {
                speciesMask = Vector3Int.one;
            }
            else
            {
                int species = Random.Range(1, numSpecies + 1);
                speciesIndex = species - 1;
                speciesMask = new Vector3Int((species == 1) ? 1 : 0, (species == 2) ? 1 : 0, (species == 3) ? 1 : 0);
            }

            agents[i] = new Agent() { position = startPos, angle = angle, age = 0f, speciesMask = speciesMask, speciesIndex = speciesIndex };
        }

        ComputeHelper.CreateAndSetBuffer<Agent>(ref agentBuffer, agents, compute, "agents", updateKernel);
        compute.SetInt("numAgents", settings.numAgents);
        drawAgentsCS.SetBuffer(0, "agents", agentBuffer);
        drawAgentsCS.SetInt("numAgents", settings.numAgents);


        compute.SetInt("width", settings.width);
        compute.SetInt("height", settings.height);


    }

    void FixedUpdate()
    {
        for (int i = 0; i < settings.stepsPerFrame; i++)
        {
            RunSimulation();
        }
    }

    void LateUpdate()
    {
        if (showAgentsOnly)
        {
            ComputeHelper.ClearRenderTexture(displayTexture);

            drawAgentsCS.SetTexture(0, "TargetTexture", displayTexture);
            ComputeHelper.Dispatch(drawAgentsCS, settings.numAgents, 1, 1, 0);

        }
        else
        {
            ComputeHelper.Dispatch(compute, settings.width, settings.height, 1, kernelIndex: colourKernel);
            //	ComputeHelper.CopyRenderTexture(trailMap, displayTexture);
        }
    }

    void RunSimulation()
    {
        var speciesSettings = settings.speciesSettings;
        ComputeHelper.CreateStructuredBuffer(ref settingsBuffer, speciesSettings);
        compute.SetBuffer(updateKernel, "speciesSettings", settingsBuffer);
        compute.SetBuffer(colourKernel, "speciesSettings", settingsBuffer);

        // Assign settings
        compute.SetFloat("deltaTime", Time.fixedDeltaTime);
        compute.SetFloat("time", Time.fixedTime);

        compute.SetFloat("trailWeight", settings.trailWeight);
        compute.SetFloat("decayRate", settings.decayRate);
        compute.SetFloat("diffuseRate", settings.diffuseRate);
        compute.SetInt("numSpecies", speciesSettings.Length);


        ComputeHelper.Dispatch(compute, settings.numAgents, 1, 1, kernelIndex: updateKernel);
        ComputeHelper.Dispatch(compute, settings.width, settings.height, 1, kernelIndex: diffuseMapKernel);

        ComputeHelper.CopyRenderTexture(diffusedTrailMap, trailMap);
    }

    void OnDestroy()
    {

        ComputeHelper.Release(agentBuffer, settingsBuffer);
    }

    public struct Agent
    {
        public Vector2 position;
        public float angle;
        public float age;
        public Vector3Int speciesMask;
        int unusedSpeciesChannel;
        public int speciesIndex;
    }


}
